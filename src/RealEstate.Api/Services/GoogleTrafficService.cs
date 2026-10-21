using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public record TrafficCondition(TrafficCongestionLevel Level, double DelayRatio);

public interface IGoogleTrafficService
{
    // Live congestion snapshot for the stretch of road between the two given points — meant to
    // be called with a nearby major road's own endpoints (NearbyRoad.Geometry), not an arbitrary
    // pair, so the measured segment is actually the road fronting the lot. Throws on failure
    // (same pattern as IRoadNetworkService/IDenueService) so the caller can log/attribute it
    // separately from "no data available."
    Task<TrafficCondition> GetTrafficConditionAsync(double originLat, double originLng, double destLat, double destLng, CancellationToken cancellationToken = default);
}

public class GoogleTrafficService : IGoogleTrafficService
{
    private const string Url = "https://routes.googleapis.com/directions/v2:computeRoutes";

    // Traffic conditions shift through the day, but this is a lead-magnet snapshot, not a live
    // dashboard — a short TTL keeps repeated loads of the same listing from re-billing the
    // (paid, per-request) Routes API on every click, same reasoning as OverpassRoadNetworkService's
    // cache, just a much shorter window given how quickly traffic actually changes.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private const int CoordinatePrecision = 3;

    private readonly HttpClient _http;
    private readonly GoogleMapsOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GoogleTrafficService> _logger;

    public GoogleTrafficService(HttpClient http, IOptions<GoogleMapsOptions> options, IMemoryCache cache, ILogger<GoogleTrafficService> logger)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public Task<TrafficCondition> GetTrafficConditionAsync(double originLat, double originLng, double destLat, double destLng, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"google-traffic:{Round(originLat)}:{Round(originLng)}:{Round(destLat)}:{Round(destLng)}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await FetchAsync(originLat, originLng, destLat, destLng, cancellationToken);
        })!;
    }

    private async Task<TrafficCondition> FetchAsync(double originLat, double originLng, double destLat, double destLng, CancellationToken cancellationToken)
    {
        var body = new
        {
            origin = new { location = new { latLng = new { latitude = originLat, longitude = originLng } } },
            destination = new { location = new { latLng = new { latitude = destLat, longitude = destLng } } },
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_AWARE"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Goog-Api-Key", _options.RoutesApiKey);
        // Field mask is required by the Routes API (no default field set) — staticDuration is
        // the free-flow baseline (no traffic), duration is the current traffic-aware estimate;
        // comparing the two is the whole signal this service exists to produce.
        request.Headers.Add("X-Goog-FieldMask", "routes.duration,routes.staticDuration");

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        // GetProperty/array-indexer throw KeyNotFoundException/ArgumentOutOfRangeException on a
        // shape the caller doesn't catch (it only expects HttpRequestException/TaskCanceledException/
        // JsonException/InvalidOperationException, same as every other best-effort lookup in
        // ViabilityAnalysisService) — an empty "routes" array (Google found no drivable route
        // between the two points) or a missing duration field would otherwise crash the whole
        // viability analysis with an unhandled exception instead of degrading gracefully.
        if (!document.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Google Routes API returned no routes.");
        }
        var route = routes[0];
        if (!route.TryGetProperty("duration", out var durationEl) || !route.TryGetProperty("staticDuration", out var staticDurationEl))
        {
            throw new InvalidOperationException("Google Routes API response is missing duration/staticDuration.");
        }
        var duration = ParseSeconds(durationEl.GetString());
        var staticDuration = ParseSeconds(staticDurationEl.GetString());

        var ratio = staticDuration > 0 ? (double)duration / staticDuration : 1.0;
        var level = ratio switch
        {
            < 1.15 => TrafficCongestionLevel.Low,
            < 1.4 => TrafficCongestionLevel.Moderate,
            _ => TrafficCongestionLevel.High
        };

        return new TrafficCondition(level, ratio);
    }

    // Routes API durations come back as a protobuf Duration string like "123s", not a number.
    private static int ParseSeconds(string? raw) =>
        raw is not null && raw.EndsWith('s') && int.TryParse(raw[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : 0;

    private static double Round(double value) => Math.Round(value, CoordinatePrecision);
}
