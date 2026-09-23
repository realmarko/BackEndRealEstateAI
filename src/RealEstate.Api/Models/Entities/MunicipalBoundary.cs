using NetTopologySuite.Geometries;

namespace RealEstate.Api.Models.Entities;

// One row per municipality — INEGI's Marco Geoestadístico "Áreas geoestadísticas municipales"
// layer (21mun.shp within the state-level Marco Geoestadístico bundle), imported once. Complete
// municipal boundary (urban + rural), unlike AgebPopulation's AGEB polygons which only cover
// urban areas — this is the layer to use for "is point X inside municipality Y" queries, not an
// aggregation of AGEBs (which would silently miss a municipality's rural edges).
public class MunicipalBoundary
{
    // 5-digit INEGI code: state(2) + municipality(3), e.g. "21042" for Coronango, Puebla.
    public string Cvegeo { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string StateName { get; set; } = null!;

    // SRID 4326 (WGS84) — reprojected at import time from the source shapefile's native Lambert
    // Conformal Conic projection, matching AgebPopulation.Boundary's convention.
    public Geometry Boundary { get; set; } = null!;
}
