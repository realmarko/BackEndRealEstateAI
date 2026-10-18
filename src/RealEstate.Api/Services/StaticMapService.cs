using System.Globalization;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public interface IStaticMapService
{
    // A rendered map image (PNG) centered on the point with a marker, for embedding in the
    // viability-report PDF (no live JS map available inside a static document). Null on any
    // failure — best-effort, same reasoning as FactSheetBuilderService's photo downloads: a
    // missing map image must never fail the PDF the lead already requested.
    Task<byte[]?> GetStaticMapAsync(double lat, double lng, CancellationToken cancellationToken = default);
}

public class StaticMapService : IStaticMapService
{
    private const string BaseUrl = "https://maps.googleapis.com/maps/api/staticmap";

    private readonly HttpClient _http;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<StaticMapService> _logger;

    public StaticMapService(HttpClient http, IOptions<GoogleMapsOptions> options, ILogger<StaticMapService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]?> GetStaticMapAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var latLng = $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
        // scale=2 for a crisp image on a printed/zoomed PDF, not just a 1x web-resolution pull.
        var url = $"{BaseUrl}?center={latLng}&zoom=15&size=600x260&scale=2&markers=color:0xc6a15b%7C{latLng}&key={_options.StaticMapsApiKey}";

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
}
