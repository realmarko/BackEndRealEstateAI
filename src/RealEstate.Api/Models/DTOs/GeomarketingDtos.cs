using System.Text.Json;

namespace RealEstate.Api.Models.DTOs;

public class BusinessDensityDto
{
    public int Count { get; set; }
}

public class MunicipalityListItemDto
{
    public string Cvegeo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class MunicipalityBoundaryDto
{
    public string Cvegeo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // A GeoJSON Geometry object (e.g. {"type":"Polygon","coordinates":[...]}) — typed as
    // JsonElement, not string, so System.Text.Json embeds it as real JSON on the way out instead
    // of double-escaping it as a quoted string.
    public JsonElement Boundary { get; set; }
}

public class AgebBoundaryDto
{
    public string Cvegeo { get; set; } = string.Empty;
    public JsonElement Boundary { get; set; }
    public string? EstimatedSocioeconomicLevel { get; set; }
}

public class PopulationDensityDto
{
    public int Population { get; set; }
    public double AreaSqKm { get; set; }
    public double DensityPerSqKm { get; set; }
    public int CensusYear { get; set; }

    // Socioeconomic proxy estimated from public INEGI Census indicators — NOT the commercial
    // AMAI NSE classification (see SocioeconomicLevel). Null for the AGEBs INEGI's own data
    // masking left with too few source indicators to estimate from.
    public double? SocioeconomicScore { get; set; }
    public string? EstimatedSocioeconomicLevel { get; set; }
}
