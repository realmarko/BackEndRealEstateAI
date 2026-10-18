using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Extensions;

namespace RealEstate.Api.Services;

public record NearbyRoad(string? Name, string Classification, double Lat, double Lng, IReadOnlyList<(double Lat, double Lng)>? Geometry = null);

public interface IRoadNetworkService
{
    // Real, mapped major roads (highway/trunk/primary/secondary) near a point — not DENUE (a
    // business registry, not road infrastructure) and not Google (no extra credential needed):
    // OpenStreetMap's Overpass API, free and keyless. Deduplicated by road name/ref (OSM splits
    // one physical road into many short "way" segments, which would otherwise list the same
    // highway several times).
    Task<List<NearbyRoad>> FindMajorRoadsNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);

    // Bus stops and intercity bus terminals — DENUE's name-based search doesn't reliably find
    // these (a terminal is named "CAPU"/"Central de Autobuses de Puebla", not anything containing
    // a generic word like "camionera"), but OSM tags them properly (highway=bus_stop,
    // amenity=bus_station), so this reuses the same Overpass plumbing as the roads lookup above.
    Task<List<NearbyRoad>> FindTransitNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);
}

public class OverpassRoadNetworkService : IRoadNetworkService
{
    // The free public overpass-api.de instance regularly returns "502 ... currently at capacity"
    // under its own documented fair-use throttling (confirmed live, 2026-10-18) — not specific to
    // this app's traffic. A third mirror was added the same day after overpass-api.de AND
    // overpass.kumi.systems were both down at once (502 / 500, confirmed live) — two mirrors
    // wasn't enough headroom. Each is the same Overpass QL/API shape, independently operated, no
    // credential needed for any of them.
    private static readonly string[] BaseUrls =
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.openstreetmap.fr/api/interpreter"
    };

    // Road networks don't change day to day — same reasoning as DenueService's cache.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private const int CoordinatePrecision = 3;

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OverpassRoadNetworkService> _logger;

    public OverpassRoadNetworkService(HttpClient http, IMemoryCache cache, ILogger<OverpassRoadNetworkService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public Task<List<NearbyRoad>> FindMajorRoadsNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
    {
        var query = $"way(around:{radiusMeters},{Coord(lat)},{Coord(lng)})[\"highway\"~\"^(motorway|trunk|primary|secondary)$\"];";
        return QueryCachedAsync("osm-roads", lat, lng, radiusMeters, query, ClassifyRoad, cancellationToken);
    }

    public Task<List<NearbyRoad>> FindTransitNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
    {
        var latStr = Coord(lat);
        var lngStr = Coord(lng);
        // bus_stop as a node (a point, not a way with length) — amenity=bus_station can be mapped
        // as either a node or an area (way), so both are queried for that one.
        var query = $"(node(around:{radiusMeters},{latStr},{lngStr})[\"highway\"=\"bus_stop\"];" +
                     $"node(around:{radiusMeters},{latStr},{lngStr})[\"amenity\"=\"bus_station\"];" +
                     $"way(around:{radiusMeters},{latStr},{lngStr})[\"amenity\"=\"bus_station\"];);";
        return QueryCachedAsync("osm-transit", lat, lng, radiusMeters, query, ClassifyTransit, cancellationToken);
    }

    private static string Coord(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string ClassifyRoad(JsonElement tags)
    {
        if (!tags.TryGetProperty("highway", out var highwayEl)) return "Vialidad";
        return highwayEl.GetString() switch
        {
            "motorway" => "Autopista",
            "trunk" => "Vía rápida",
            "primary" => "Avenida/carretera principal",
            "secondary" => "Avenida secundaria",
            _ => "Vialidad"
        };
    }

    private static string ClassifyTransit(JsonElement tags) =>
        tags.TryGetProperty("amenity", out var amenityEl) && amenityEl.GetString() == "bus_station"
            ? "Central de autobuses"
            : "Parada de autobús";

    private Task<List<NearbyRoad>> QueryCachedAsync(
        string cachePrefix, double lat, double lng, int radiusMeters, string overpassBody, Func<JsonElement, string> classify, CancellationToken cancellationToken)
    {
        var cacheKey = $"{cachePrefix}:{Math.Round(lat, CoordinatePrecision)}:{Math.Round(lng, CoordinatePrecision)}:{radiusMeters}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await FetchAsync(lat, lng, overpassBody, classify, cancellationToken);
        })!;
    }

    private async Task<List<NearbyRoad>> FetchAsync(double lat, double lng, string overpassBody, Func<JsonElement, string> classify, CancellationToken cancellationToken)
    {
        // "geom" adds each way's full node-by-node shape (used to draw the highlighted road line
        // on the map) on top of "center"/"tags", which the rest of this method still relies on —
        // a bus-stop node has no shape, so geom is simply absent for those elements.
        var query = $"[out:json][timeout:10];{overpassBody}out tags geom center;";

        Exception? lastError = null;
        foreach (var baseUrl in BaseUrls)
        {
            try
            {
                return await FetchFromAsync(baseUrl, query, lat, lng, classify, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Overpass mirror {BaseUrl} failed, trying next if any", baseUrl);
            }
        }

        throw lastError ?? new InvalidOperationException("No Overpass mirrors configured.");
    }

    private async Task<List<NearbyRoad>> FetchFromAsync(string baseUrl, string query, double lat, double lng, Func<JsonElement, string> classify, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}?data={Uri.EscapeDataString(query)}");
        // Overpass's own usage policy asks for an identifying User-Agent — a generic/missing one
        // gets a 406 (confirmed live), not a silent pass-through.
        request.Headers.UserAgent.ParseAdd("EspacialComMx-ViabilityReport/1.0 (+https://espacial.com.mx)");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("elements", out var elements))
            return new List<NearbyRoad>();

        var byKey = new Dictionary<string, NearbyRoad>();
        foreach (var element in elements.EnumerateArray())
        {
            // A "way" (queried with `out ... center`) carries its midpoint under "center"; a
            // "node" (a bus stop is a single point, not a shape) carries its position directly as
            // top-level "lat"/"lon" — Overpass never adds a "center" wrapper for a node.
            double elementLat, elementLng;
            if (element.TryGetProperty("center", out var center))
            {
                elementLat = center.GetProperty("lat").GetDouble();
                elementLng = center.GetProperty("lon").GetDouble();
            }
            else if (element.TryGetProperty("lat", out var latEl) && element.TryGetProperty("lon", out var lonEl))
            {
                elementLat = latEl.GetDouble();
                elementLng = lonEl.GetDouble();
            }
            else
            {
                continue;
            }

            string? name = null;
            var classification = "Vialidad";
            if (element.TryGetProperty("tags", out var tags))
            {
                if (tags.TryGetProperty("name", out var nameEl)) name = nameEl.GetString();
                else if (tags.TryGetProperty("ref", out var refEl)) name = refEl.GetString();
                classification = classify(tags);
            }

            List<(double Lat, double Lng)>? geometry = null;
            if (element.TryGetProperty("geometry", out var geometryEl) && geometryEl.ValueKind == JsonValueKind.Array)
            {
                geometry = geometryEl.EnumerateArray()
                    .Select(p => (p.GetProperty("lat").GetDouble(), p.GetProperty("lon").GetDouble()))
                    .ToList();
            }

            // OSM represents one physical feature as several elements/segments — key on name
            // (falling back to classification+rounded position for an unnamed one) and keep only
            // the one closest to the search point, not just the first one encountered.
            var key = name ?? $"{classification}:{Math.Round(elementLat, 3)}:{Math.Round(elementLng, 3)}";
            if (!byKey.TryGetValue(key, out var existing) || GeoFactory.DistanceMeters(lat, lng, elementLat, elementLng) < GeoFactory.DistanceMeters(lat, lng, existing.Lat, existing.Lng))
            {
                byKey[key] = new NearbyRoad(name, classification, elementLat, elementLng, geometry);
            }
        }

        return byKey.Values.ToList();
    }
}
