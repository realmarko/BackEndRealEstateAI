using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public record GeoCoordinates(double Lat, double Lng);

public interface IGoogleMapsLinkResolverService
{
    // Best-effort: looks for a Google Maps link anywhere in the pasted ad text (short
    // maps.app.goo.gl share links, or a full google.com/maps URL) and tries to turn it into real
    // coordinates. Never throws — any failure (no link found, redirect failed, no coordinates
    // resolvable) just returns null, same convention as every other external lookup in this
    // codebase, so a bad/unsupported link never fails the rest of the draft parse.
    Task<GeoCoordinates?> TryResolveAsync(string text, CancellationToken cancellationToken = default);
}

public class GoogleMapsLinkResolverService : IGoogleMapsLinkResolverService
{
    private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";

    // Matches any Google Maps link, short or long — share links from the mobile app always use
    // maps.app.goo.gl; older/desktop shares use goo.gl/maps or g.co/maps; a pasted full URL
    // (copied straight from the address bar) uses google.com/maps or maps.google.com.
    private static readonly Regex MapsUrlPattern = new(
        @"https?://(?:maps\.app\.goo\.gl|goo\.gl/maps|g\.co/maps|(?:www\.)?google\.[a-z.]+/maps|maps\.google\.[a-z.]+)/\S+",
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

    public async Task<GeoCoordinates?> TryResolveAsync(string text, CancellationToken cancellationToken = default)
    {
        var match = MapsUrlPattern.Match(text);
        if (!match.Success) return null;

        try
        {
            // Checked on the URL exactly as pasted, before paying for any network call — a full
            // (not shortened) Maps URL copied straight from the address bar already has this, so
            // a redirect-follow just to re-fetch the identical URL would be wasted work.
            if (TryExtractDirectCoordinates(match.Value) is { } directFromPastedUrl) return directFromPastedUrl;

            var resolvedUrl = await ResolveRedirectAsync(match.Value, cancellationToken);

            if (TryExtractDirectCoordinates(resolvedUrl) is { } direct) return direct;

            var placeMatch = PlaceNamePattern.Match(resolvedUrl);
            if (!placeMatch.Success) return null;

            var placeName = Uri.UnescapeDataString(placeMatch.Groups[1].Value.Replace('+', ' '));
            return await GeocodeAsync(placeName, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Google Maps link resolution failed for {Url}", match.Value);
            return null;
        }
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
