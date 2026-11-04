using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public record NearbyInfrastructureProject(
    Guid Id, string Name, string? Description, InfrastructureProjectType Type, InfrastructureProjectStatus Status,
    DateTime AnnouncedDate, DateTime? ExpectedCompletionDate, string? SourceUrl, int DistanceMeters);

// Shared by ViabilityAnalysisService (shown as its own report section) and
// AppreciationPotentialService (a "potencial de plusvalía" signal) — both need "non-cancelled
// InfrastructureProjects within radiusMeters of a point". Same bbox-prefilter-then-exact-distance
// shape as FraccionamientoProximity.FindNearbyAsync, for the same reason: InfrastructureProject
// has plain Latitude/Longitude columns, no PostGIS geography column to query in one round trip.
public static class InfrastructureProjectProximity
{
    // Single source for the two callers (ViabilityAnalysisService, AppreciationPotentialService)
    // that previously each declared this same value independently — kept here instead so tuning
    // it can't happen in one place and silently drift out of sync with the other.
    public const double DefaultRadiusMeters = 5000;


    // radiusMeters is an outer search cap, not the per-project cutoff: the bbox prefilter can
    // only widen by so much before every call becomes a near-table-scan, so it bounds how far
    // out this ever looks. Within that window, each project's own AffectedRadiusMeters (set by
    // the admin, e.g. 500m for a single new school vs. 20000m for a highway corridor) is what
    // actually decides whether it counts as "nearby" — min() of the two, so a project can only
    // narrow its effective radius below the cap, never ask this call to search further than it's
    // willing to.
    public static async Task<List<NearbyInfrastructureProject>> FindNearbyAsync(
        ApplicationDbContext db, double lat, double lng, double radiusMeters, CancellationToken cancellationToken = default)
    {
        var (latDelta, lngDelta) = GeoFactory.ApproxDegreeDelta(lat, radiusMeters);

        var candidates = await db.InfrastructureProjects.AsNoTracking()
            .Where(p => p.Status != InfrastructureProjectStatus.Cancelled)
            .Where(p => p.Latitude >= lat - latDelta && p.Latitude <= lat + latDelta)
            .Where(p => p.Longitude >= lng - lngDelta && p.Longitude <= lng + lngDelta)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(p => new
            {
                Project = p,
                DistanceMeters = (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, p.Latitude, p.Longitude))
            })
            .Where(x => x.DistanceMeters <= Math.Min(radiusMeters, x.Project.AffectedRadiusMeters))
            .OrderBy(x => x.DistanceMeters)
            .Select(x => new NearbyInfrastructureProject(
                x.Project.Id, x.Project.Name, x.Project.Description, x.Project.Type, x.Project.Status,
                x.Project.AnnouncedDate, x.Project.ExpectedCompletionDate, x.Project.SourceUrl, x.DistanceMeters))
            .ToList();
    }
}
