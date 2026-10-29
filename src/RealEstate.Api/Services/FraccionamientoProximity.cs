using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public record NearbyFraccionamiento(string Name, string? Stage, int DistanceMeters);

// Shared by ViabilityAnalysisService (the "nuevo desarrollo residencial cercano" viability bonus)
// and AppreciationPotentialService (the new-development appreciation signal) — both need
// "Published Fraccionamientos within radiusMeters of a point", previously copy-pasted in each
// with its own constant and projection. Centralized so a future change to the radius, status
// filter, or the bbox-prefilter approach only has to happen once.
public static class FraccionamientoProximity
{
    // Bbox-prefilter-then-exact-distance, same approach as
    // FraccionamientoIngestionService.FindMatchAsync — no PostGIS geography column on
    // Fraccionamiento (plain Latitude/Longitude) to do this in one query.
    public static async Task<List<NearbyFraccionamiento>> FindNearbyAsync(
        ApplicationDbContext db, double lat, double lng, double radiusMeters, CancellationToken cancellationToken = default)
    {
        var (latDelta, lngDelta) = GeoFactory.ApproxDegreeDelta(lat, radiusMeters);

        var candidates = await db.Fraccionamientos.AsNoTracking()
            .Where(f => f.Status == FraccionamientoStatus.Published)
            .Where(f => f.Latitude >= lat - latDelta && f.Latitude <= lat + latDelta)
            .Where(f => f.Longitude >= lng - lngDelta && f.Longitude <= lng + lngDelta)
            .Select(f => new { f.Name, f.Stage, f.Latitude, f.Longitude })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(f => new NearbyFraccionamiento(f.Name, f.Stage, (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, f.Latitude, f.Longitude))))
            .Where(f => f.DistanceMeters <= radiusMeters)
            .OrderBy(f => f.DistanceMeters)
            .ToList();
    }
}
