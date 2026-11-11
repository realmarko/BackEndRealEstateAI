using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public record NearbyListing(Listing Listing, int DistanceMeters);

// Radius search over real Listings rows — same bbox-prefilter-then-exact-distance shape as
// FraccionamientoProximity.FindNearbyAsync (no PostGIS radius query over Listing.Location is used
// here: that column is a plain geometry(Point,4326), so ST_DWithin against it would need the
// distance in degrees, not meters — the lat/lng-bbox approach this app already uses elsewhere
// stays correct regardless). Built for ResidentialAnalysisService's comparables/rental-yield
// lookups, which need "real listings of the same type near this point", not a bounding-box
// viewport query like ListingsController.Search's map-pan filter.
public static class ListingProximity
{
    public static async Task<List<NearbyListing>> FindNearbyAsync(
        ApplicationDbContext db,
        Guid excludeListingId,
        double lat,
        double lng,
        double radiusMeters,
        ListingType listingType,
        PropertyType propertyType,
        CancellationToken cancellationToken = default)
    {
        var (latDelta, lngDelta) = GeoFactory.ApproxDegreeDelta(lat, radiusMeters);

        var candidates = await db.Listings.AsNoTracking()
            .Where(l => l.Id != excludeListingId)
            .Where(l => l.Status != ListingStatus.Removed)
            .Where(l => l.ListingType == listingType && l.PropertyType == propertyType)
            .Where(l => l.Latitude >= lat - latDelta && l.Latitude <= lat + latDelta)
            .Where(l => l.Longitude >= lng - lngDelta && l.Longitude <= lng + lngDelta)
            .Include(l => l.Address)
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .ToListAsync(cancellationToken);

        return candidates
            .Select(l => new NearbyListing(l, (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, l.Latitude, l.Longitude))))
            .Where(n => n.DistanceMeters <= radiusMeters)
            .OrderBy(n => n.DistanceMeters)
            .ToList();
    }
}
