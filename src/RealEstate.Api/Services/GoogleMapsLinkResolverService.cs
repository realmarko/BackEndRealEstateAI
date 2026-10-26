using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public record GeoCoordinates(double Lat, double Lng);

// Coordinates is set only on success; LinkFound distinguishes "no Maps link was in the text at
// all" (LinkFound: false — nothing to report to the visitor) from "a link was there but couldn't
// be turned into coordinates" (LinkFound: true, Coordinates: null — ListingsController surfaces
// this as ListingDraftParseResultDto.MapsLinkResolutionFailed so the frontend can warn instead of
// silently leaving the location unset).
public record MapsLinkResolution(GeoCoordinates? Coordinates, bool LinkFound);

public interface IGoogleMapsLinkResolverService
{
    // Best-effort: looks for a Google Maps link anywhere in the pasted ad text (short
    // maps.app.goo.gl share links, a full google.com/maps URL, or either wrapped in a Facebook
    // "l.facebook.com/l.php?u=..." redirect) and tries to turn it into real coordinates. Never
    // throws — any failure still returns a result (never an exception), same convention as every
    // other external lookup in this codebase, so a bad/unsupported link never fails the rest of
    // the draft parse.
    Task<MapsLinkResolution> TryResolveAsync(string text, CancellationToken cancellationToken = default);
}

public class GoogleMapsLinkResolverService : IGoogleMapsLinkResolverService
{
    private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";

    // Matches a Google Maps link, short or long, anywhere in free text — share links from the
    // mobile app always use maps.app.goo.gl; older/desktop shares use goo.gl/maps or g.co/maps; a
    // pasted full URL (copied straight from the address bar) uses google.com/maps or
    // maps.google.com.
    private static readonly Regex MapsUrlPattern = new(
        @"https?://(?:maps\.app\.goo\.gl|goo\.gl/maps|g\.co/maps|(?:www\.)?google\.[a-z.]+/maps|maps\.google\.[a-z.]+)\S*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Same hosts as above, but anchored to the start of the string — used to validate a URL
    // that's already been isolated (e.g. unwrapped from a Facebook redirect), where there's no
    // surrounding text left to search through.
    private static readonly Regex MapsHostPattern = new(
        @"^https?://(?:maps\.app\.goo\.gl|goo\.gl/maps|g\.co/maps|(?:www\.)?google\.[a-z.]+/maps|maps\.google\.[a-z.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Facebook rewrites every outbound link a user posts into one of these "leaving Facebook"
    // redirect wrappers, with the real destination URL-encoded in its own "u" query parameter —
    // this is what a Maps link looks like after being shared/pasted from a Facebook post or
    // Messenger chat, rather than copied directly from the Maps app.
    private static readonly Regex FacebookWrapperPattern = new(
        @"https?://l(?:m)?\.facebook\.com/l\.php\?\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // The maps.app.goo.gl short code is alphanumeric only — isolating just host+shortcode drops
    // anything Facebook (or whatever client built the original message) glued onto the end of
    // the URL without a proper separator. Confirmed live: a real share link's "u" param decoded
    // to ".../2DWHtjKh5y3cQWqJ6📲?fbclid=..." — an emoji pasted immediately after the shortcode,
    // with no "?" before it, which 404s as-is but resolves fine once trimmed back to the
    // shortcode itself.
    private static readonly Regex ShortLinkPattern = new(
        @"https?://(?:maps\.app\.goo\.gl|goo\.gl/maps|g\.co/maps)/[A-Za-z0-9_-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A share link that already drops a bare pin (no named place attached) embeds the
    // coordinates directly in the URL in one of these shapes — checked first since it needs no
    // network call at all. A "place" share (a named address/business) instead resolves to a CID
    // reference with no coordinates in the URL, which falls through to geocoding the place's
    // address text below.
    private static readonly Regex[] DirectCoordinatePatterns =
    {
        new(@"!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)", RegexOptions.Compiled), // the pin's own exact location
        new(@"[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)", RegexOptions.Compiled),
        new(@"[?&]ll=(-?\d+\.\d+),(-?\d+\.\d+)", RegexOptions.Compiled),
        new(@"/@(-?\d+\.\d+),(-?\d+\.\d+)", RegexOptions.Compiled) // the map's own center, not necessarily the pin
    };

    // The human-readable name/address Google puts in the URL path for a "place" share, e.g.
    // ".../maps/place/Residencial+Tlatelcos,+24+ote+%231227.../data=...". Captured so it can be
    // geocoded below when no direct coordinates are present.
    private static readonly Regex PlaceNamePattern = new(@"/maps/place/([^/]+)/", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleMapsLinkResolverService> _logger;

    public GoogleMapsLinkResolverService(HttpClient http, IOptions<GoogleMapsOptions> options, ILogger<GoogleMapsLinkResolverService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MapsLinkResolution> TryResolveAsync(string text, CancellationToken cancellationToken = default)
    {
        var candidateUrl = ExtractCandidateUrl(text);
        if (candidateUrl is null) return new MapsLinkResolution(null, false);

        try
        {
            // Checked on the URL exactly as pasted, before paying for any network call — a full
            // (not shortened) Maps URL copied straight from the address bar already has this, so
            // a redirect-follow just to re-fetch the identical URL would be wasted work.
            if (TryExtractDirectCoordinates(candidateUrl) is { } directFromPastedUrl)
            {
                return new MapsLinkResolution(directFromPastedUrl, true);
            }

            var resolvedUrl = await ResolveRedirectAsync(candidateUrl, cancellationToken);

            if (TryExtractDirectCoordinates(resolvedUrl) is { } direct) return new MapsLinkResolution(direct, true);

            var placeMatch = PlaceNamePattern.Match(resolvedUrl);
            if (!placeMatch.Success) return new MapsLinkResolution(null, true);

            var placeName = Uri.UnescapeDataString(placeMatch.Groups[1].Value.Replace('+', ' '));
            var geocoded = await GeocodeAsync(placeName, cancellationToken);
            return new MapsLinkResolution(geocoded, true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Google Maps link resolution failed for {Url}", candidateUrl);
            return new MapsLinkResolution(null, true);
        }
    }

    // Finds the actual Maps URL to resolve, unwrapping a Facebook redirect first when present —
    // Facebook link-shares are checked first since the "u" param they carry is itself a full URL
    // that would otherwise also satisfy MapsUrlPattern's own generic \S* tail and get picked up
    // mangled, with l.facebook.com as the (wrong) host to resolve.
    private static string? ExtractCandidateUrl(string text)
    {
        var facebookMatch = FacebookWrapperPattern.Match(text);
        if (facebookMatch.Success)
        {
            var unwrapped = UnwrapFacebookLink(facebookMatch.Value);
            if (unwrapped is not null && MapsHostPattern.IsMatch(unwrapped))
            {
                return CleanGoogleMapsUrl(unwrapped);
            }
            // Facebook wrapped some other, non-Maps link — fall through in case the text also has
            // a bare Maps URL elsewhere (e.g. restated outside the Facebook share).
        }

        var directMatch = MapsUrlPattern.Match(text);
        return directMatch.Success ? CleanGoogleMapsUrl(directMatch.Value) : null;
    }

    private static string? UnwrapFacebookLink(string facebookUrl)
    {
        // TryCreate, not `new Uri(...)`: this runs inside ExtractCandidateUrl, which is called
        // BEFORE TryResolveAsync's own try/catch starts — a malformed link throwing here would
        // propagate uncaught out of a method documented as "never throws", through the awaited
        // task in ListingsController.ParseDraft, as an exception type its catch filter doesn't
        // list, crashing the whole draft-parse request instead of just leaving coordinates unset.
        if (!Uri.TryCreate(facebookUrl, UriKind.Absolute, out var uri)) return null;
        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue("u", out var target) ? target.ToString() : null;
    }

    // No-op for anything that isn't a maps.app.goo.gl/goo.gl/g.co short link — only those are
    // known to get corrupted this way in practice; a full google.com/maps URL's coordinates are
    // self-contained further down the string regardless of what's appended after it.
    private static string CleanGoogleMapsUrl(string url)
    {
        var shortMatch = ShortLinkPattern.Match(url);
        return shortMatch.Success ? shortMatch.Value : url;
    }

    // Lets HttpClient follow every redirect automatically (its default behavior) — the short
    // link's whole purpose is a redirect chain, so the final RequestUri after that chain is the
    // real, long-form Maps URL.
    private async Task<string> ResolveRedirectAsync(string shortUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, shortUrl);
        // Google's redirect/consent handling differs for requests with no recognizable browser
        // User-Agent — this mirrors a normal desktop browser so the chain resolves the same way
        // it would for the person who pasted the link.
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");

        using var response = await _http.SendAsync(request, cancellationToken);
        return response.RequestMessage?.RequestUri?.ToString() ?? shortUrl;
    }

    private static GeoCoordinates? TryExtractDirectCoordinates(string url)
    {
        foreach (var pattern in DirectCoordinatePatterns)
        {
            var match = pattern.Match(url);
            if (match.Success
                && double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                return new GeoCoordinates(lat, lng);
            }
        }
        return null;
    }

    private async Task<GeoCoordinates?> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var url = $"{GeocodeUrl}?address={Uri.EscapeDataString(address)}&key={_options.GeocodingApiKey}";
        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "OK") return null;
        if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;

        var location = results[0].GetProperty("geometry").GetProperty("location");
        return new GeoCoordinates(location.GetProperty("lat").GetDouble(), location.GetProperty("lng").GetDouble());
    }
}
