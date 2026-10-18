namespace RealEstate.Api.Models.DTOs;

public class BusinessTypeCriteriaDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double MinFrontageMeters { get; set; }
    public double MinLotSizeSqm { get; set; }
    public bool? PrefersCornerLot { get; set; }
    public bool PrefersPrimaryRoadAccess { get; set; }
    public double? PopulationRadiusKm { get; set; }
    public int? MinPopulationInRadius { get; set; }
    public int? CompetitorExclusionRadiusMeters { get; set; }
    public string Notes { get; set; } = string.Empty;
}

// Deliberately excludes Key/DisplayName/DenueSearchTerm/GooglePlacesType — those wire the row to
// its DENUE/Places lookup and frontend label; only the numeric thresholds and Notes are meant to
// be tuned at runtime (see BusinessTypeCriteria's own doc comment).
public class BusinessTypeCriteriaUpdateDto
{
    public double MinFrontageMeters { get; set; }
    public double MinLotSizeSqm { get; set; }
    public bool? PrefersCornerLot { get; set; }
    public bool PrefersPrimaryRoadAccess { get; set; }
    public double? PopulationRadiusKm { get; set; }
    public int? MinPopulationInRadius { get; set; }
    public int? CompetitorExclusionRadiusMeters { get; set; }
    public string Notes { get; set; } = string.Empty;
}

// One business type's fit for a given point/listing — the "why" behind the score, not just the
// number, so the frontend/PDF can explain each check instead of showing an opaque percentage.
public class BusinessViabilityDto
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    // 0-100. See ViabilityAnalysisService for the weighting.
    public int Score { get; set; }
    public List<ViabilityCheckDto> Checks { get; set; } = new();
}

public class ViabilityCheckDto
{
    public string Label { get; set; } = string.Empty;
    // true = meets the criterion, false = fails it, null = criterion not applicable/not
    // evaluated for this business type (e.g. population check for a gas station).
    public bool? Passed { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class ViabilityReportRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class ViabilityAnalysisDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int? PopulationInRadius { get; set; }
    public double? SocioeconomicScore { get; set; }
    public bool PopulationDataAvailable { get; set; }
    public List<BusinessViabilityDto> Businesses { get; set; } = new();
}
