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
    public string StateName { get; set; } = string.Empty;
}

public class StateListItemDto
{
    public string Code { get; set; } = string.Empty;
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

public class AgebCompetitorDensityDto
{
    public string Cvegeo { get; set; } = string.Empty;
    public JsonElement Boundary { get; set; }
    // Count at this AGEB's centroid, not a true area aggregate (see GeomarketingController's
    // endpoint comment) — null means the DENUE lookup failed for this one AGEB, shown distinct
    // from a genuine 0.
    public int? CompetitorCount { get; set; }
}

public class AppreciationPotentialDto
{
    public string Cvegeo { get; set; } = string.Empty;
    public JsonElement Boundary { get; set; }

    // Null means no signal had enough data for this AGEB — distinct from a genuine low score of 0,
    // which would wrongly read as "bad potential" instead of "unknown".
    public double? Score { get; set; }

    // One human-readable line per signal that contributed to Score, so the number is never a
    // black box — mirrors ViabilityCheckDto.Detail's role in ViabilityAnalysisService.
    public List<string> Explanations { get; set; } = new();
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
