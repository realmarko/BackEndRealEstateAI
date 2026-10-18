using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public record StaticMapMarker(double Lat, double Lng, string Color);

public interface IStaticMapService
{
    // A rendered map image (PNG) centered on the point with a marker, for embedding in the
    // viability-report PDF (no live JS map available inside a static document). extraMarkers
    // plots the same traffic-generator/road points the interactive page map shows — when present,
    // the map auto-fits to include all of them instead of staying fixed on the listing alone.
    // Null on any failure — best-effort, same reasoning as FactSheetBuilderService's photo
    // downloads: a missing map image must never fail the PDF the lead already requested.
    Task<byte[]?> GetStaticMapAsync(double lat, double lng, IReadOnlyList<StaticMapMarker>? extraMarkers = null, CancellationToken cancellationToken = default);
}

public class StaticMapService : IStaticMapService
{
    private const string BaseUrl = "https://maps.googleapis.com/maps/api/staticmap";

    // Google Static Maps only allows so many markers before the URL/response gets unwieldy on a
    // printed page — same reasoning as the interactive map's own per-category caps, just applied
    // across all extra markers combined here.
    private const int MaxExtraMarkers = 10;

    private readonly HttpClient _http;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<StaticMapService> _logger;

    public StaticMapService(HttpClient http, IOptions<GoogleMapsOptions> options, ILogger<StaticMapService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]?> GetStaticMapAsync(double lat, double lng, IReadOnlyList<StaticMapMarker>? extraMarkers = null, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(lat, lng, extraMarkers);

        try
        {
            return await _http.GetByteArrayAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch static map for {Lat},{Lng}", lat, lng);
            return null;
        }
    }

    private string BuildUrl(double lat, double lng, IReadOnlyList<StaticMapMarker>? extraMarkers)
    {
        var listingLatLng = Coord(lat, lng);
        var sb = new StringBuilder(BaseUrl).Append("?size=600x260&scale=2");

        var markers = (extraMarkers ?? Array.Empty<StaticMapMarker>()).Take(MaxExtraMarkers).ToList();
        if (markers.Count == 0)
        {
            // Single point: fixed center/zoom, same as before — nothing to fit bounds around.
            sb.Append($"&center={listingLatLng}&zoom=15");
        }
        // With extra markers, center/zoom are deliberately omitted — Static Maps auto-computes a
        // bounding viewport that fits every marker when both are left out, same effect as the
        // interactive map's map.fitBounds(...).

        sb.Append($"&markers=color:0xc6a15b%7C{listingLatLng}");

        foreach (var marker in markers)
        {
            sb.Append($"&markers=color:{Uri.EscapeDataString(marker.Color)}%7C{Coord(marker.Lat, marker.Lng)}");
        }

        sb.Append($"&key={_options.StaticMapsApiKey}");
        return sb.ToString();
    }

    private static string Coord(double lat, double lng) =>
        $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
}
