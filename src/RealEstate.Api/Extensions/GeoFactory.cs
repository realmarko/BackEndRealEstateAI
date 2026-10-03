using NetTopologySuite.Geometries;

namespace RealEstate.Api.Extensions;

// Every geometry the app builds server-side (AGEB lookup points, bbox queries) is WGS84 (SRID
// 4326) with the default precision model — one shared factory means that never has to be
// re-decided (or re-typo'd) per file.
public static class GeoFactory
{
    public static readonly GeometryFactory Instance = new(new PrecisionModel(), 4326);

    // Shared by every endpoint that takes a map-viewport bounding box (GeomarketingController's
    // AGEB search, ListingsController's listing search) — both need the same 5-point closed-ring
    // polygon in the same winding order, and both need the same swLat<neLat / swLng<neLng guard:
    // without it, inverted corners (e.g. a pan across the antimeridian) build a self-crossing
    // polygon that PostGIS/GEOS can throw on, turning what should be an empty result into an
    // unhandled 500 instead of the plain range filter's old graceful "zero rows".
    public static Polygon CreateBoundingBox(double swLat, double swLng, double neLat, double neLng)
    {
        if (swLat >= neLat) throw new ArgumentException("swLat must be less than neLat.");
        if (swLng >= neLng) throw new ArgumentException("swLng must be less than neLng.");

        return Instance.CreatePolygon(new[]
        {
            new Coordinate(swLng, swLat),
            new Coordinate(neLng, swLat),
            new Coordinate(neLng, neLat),
            new Coordinate(swLng, neLat),
            new Coordinate(swLng, swLat)
        });
    }
}
