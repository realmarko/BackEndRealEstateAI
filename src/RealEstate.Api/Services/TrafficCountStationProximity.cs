using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;

namespace RealEstate.Api.Services;

public record NearbyTrafficCountStation(
    string RouteName, string? RouteKey, string State, int Tdpa, string Period, int DistanceMeters);

// Used by ViabilityAnalysisService to show the nearest SICT federal-highway traffic-count
// station's real TDPA as informational context in the commercial viability report. Same
// bbox-prefilter-then-exact-distance shape as FraccionamientoProximity/
// InfrastructureProjectProximity — TrafficCountStation has plain Latitude/Longitude columns, no
// PostGIS geography column to query in one round trip.
public static class TrafficCountStationProximity
{
    // Attributing a highway's measured traffic to a specific lot stops being meaningful much
    // beyond this — unlike InfrastructureProjectProximity's admin-tunable AffectedRadiusMeters,
    // there's no per-station radius here, just one fixed outer cap.
    public const double DefaultRadiusMeters = 2000;

    // Returns only the single nearest station, not a list — a listing sits near one highway
    // segment, so showing several different TDPA readings for the same point would read as
    // conflicting rather than informative.
    public static async Task<NearbyTrafficCountStation?> FindNearestAsync(
        ApplicationDbContext db, double lat, double lng, double radiusMeters, CancellationToken cancellationToken = default)
    {
        var (latDelta, lngDelta) = GeoFactory.ApproxDegreeDelta(lat, radiusMeters);

        var candidates = await db.TrafficCountStations.AsNoTracking()
            .Where(s => s.Latitude >= lat - latDelta && s.Latitude <= lat + latDelta)
            .Where(s => s.Longitude >= lng - lngDelta && s.Longitude <= lng + lngDelta)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(s => new
            {
                Station = s,
                DistanceMeters = (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, s.Latitude, s.Longitude))
            })
            .Where(x => x.DistanceMeters <= radiusMeters)
            .OrderBy(x => x.DistanceMeters)
            .Select(x => new NearbyTrafficCountStation(
                x.Station.RouteName, x.Station.RouteKey, x.Station.State, x.Station.Tdpa, x.Station.Period, x.DistanceMeters))
            .FirstOrDefault();
    }
}
