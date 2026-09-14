namespace RealEstate.Api.Services;

// INEGI's DENUE (Directorio Estadístico Nacional de Unidades Económicas) — Mexico's official
// business registry, searchable by radius around a point. NOT a population/census data source
// (that's a separate INEGI product — Censo de Población / Marco Geoestadístico — distributed as
// geospatial files, not a simple radius-query REST API, so it isn't covered by this service).
// The token is free: register an email at
// https://www.inegi.org.mx/app/api/denue/v1/tokenVerify.aspx and it's emailed automatically.
public class DenueOptions
{
    public string Token { get; set; } = "CHANGE_ME_INEGI_DENUE_TOKEN";
}
