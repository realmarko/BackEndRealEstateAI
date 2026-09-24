using NetTopologySuite.Geometries;

namespace RealEstate.Api.Extensions;

// Every geometry the app builds server-side (AGEB lookup points, bbox queries) is WGS84 (SRID
// 4326) with the default precision model — one shared factory means that never has to be
// re-decided (or re-typo'd) per file.
public static class GeoFactory
{
    public static readonly GeometryFactory Instance = new(new PrecisionModel(), 4326);
}
