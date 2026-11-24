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

    // How long a mirror gets to answer before FetchAsync hedges by also starting the next one —
    // see FetchAsync's own comment for why this is hedging, not a bare race or sequential retry.
    private static readonly TimeSpan HedgeDelay = TimeSpan.FromSeconds(2);

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
        // on the map); deliberately NOT combined with "center" here — overpass.openstreetmap.fr
        // (confirmed live, 2026-10-18) treats geom/center as mutually exclusive and silently
        // returns center-only when both are requested together, dropping geometry entirely, even
        // though the two reference instances (overpass-api.de, overpass.kumi.systems) return
        // both just fine. FetchFromAsync derives a way's position from its own geometry instead
        // of relying on the server's "center", so this works the same across every mirror.
        var query = $"[out:json][timeout:10];{overpassBody}out tags geom;";

        // Hedged mirror requests, not a bare sequential retry and NOT a naive always-race-all-3
        // either (an earlier version of this fix did that — reviewed and reverted: it triples
        // Overpass load on every single lookup, even the common case where the first mirror
        // answers fine, which is exactly the kind of extra load that trips these mirrors'
        // documented "at capacity" throttling in the first place). Instead: start with just ONE
        // mirror — same request volume as before this change in the common/healthy case — and
        // only reach for the next mirror if the current one hasn't answered within HedgeDelay.
        // Sequential-with-full-timeout-per-mirror (the old behavior) could let a single slow
        // mirror eat its entire ~10s HttpClient timeout before the next one even got a chance,
        // easily blowing past AppreciationPotentialService.ConnectivitySignalTimeout's budget for
        // the WHOLE connectivity signal — confirmed live 2026-11-24: an AGEB's connectivity
        // signal was excluded despite real major roads existing at that point, purely because the
        // first-tried mirror was slow that day. Hedging bounds the cost of a slow mirror to
        // HedgeDelay instead of its full timeout, without paying 3x load for every lookup.
        using var raceCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, raceCts.Token);

        var fetches = new List<Task<List<NearbyRoad>>>();
        var mirrorsLaunched = 0;
        Exception? lastError = null;

        void LaunchNextMirror()
        {
            fetches.Add(FetchFromAsync(BaseUrls[mirrorsLaunched], query, lat, lng, classify, linkedCts.Token));
            mirrorsLaunched++;
        }

        LaunchNextMirror();

        while (true)
        {
            if (fetches.Count == 0)
            {
                // Every launched mirror has already failed. Hedge immediately rather than waiting
                // out a full HedgeDelay with nothing in flight — only reachable when a mirror
                // fails fast (e.g. DNS/connection refused), not when one is merely slow.
                if (mirrorsLaunched >= BaseUrls.Length)
                    throw lastError ?? new InvalidOperationException("No Overpass mirrors configured.");
                LaunchNextMirror();
                continue;
            }

            Task? hedgeTimer = mirrorsLaunched < BaseUrls.Length ? Task.Delay(HedgeDelay, linkedCts.Token) : null;
            var toWaitOn = hedgeTimer is null ? fetches.Cast<Task>() : fetches.Cast<Task>().Append(hedgeTimer);
            var winner = await Task.WhenAny(toWaitOn);

            if (winner == hedgeTimer)
            {
                LaunchNextMirror();
                continue;
            }

            var completedFetch = fetches.First(f => f == winner);
            fetches.Remove(completedFetch);
            try
            {
                var result = await completedFetch;
                raceCts.Cancel(); // Any other in-flight mirror request is no longer needed.
                return result;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "An Overpass mirror failed, hedging to the next one if any remain");
            }
        }
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
            // A "node" (a bus stop is a single point, not a shape) carries its position directly
            // as top-level "lat"/"lon". A "way" has no such top-level position — its location is
            // derived below from its own "geometry" (not a server-provided "center"; see FetchAsync
            // for why), as the midpoint of its shape.
            List<(double Lat, double Lng)>? geometry = null;
            if (element.TryGetProperty("geometry", out var geometryEl) && geometryEl.ValueKind == JsonValueKind.Array)
            {
                geometry = geometryEl.EnumerateArray()
                    .Select(p => (p.GetProperty("lat").GetDouble(), p.GetProperty("lon").GetDouble()))
                    .ToList();
            }

            double elementLat, elementLng;
            if (element.TryGetProperty("lat", out var latEl) && element.TryGetProperty("lon", out var lonEl))
            {
                elementLat = latEl.GetDouble();
                elementLng = lonEl.GetDouble();
            }
            else if (geometry is { Count: > 0 })
            {
                var mid = geometry[geometry.Count / 2];
                (elementLat, elementLng) = mid;
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
