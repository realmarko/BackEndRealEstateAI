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
    public double? MinCusRatio { get; set; }
    public string AcceptableLandUseCategories { get; set; } = string.Empty;
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
    public double? MinCusRatio { get; set; }
    public string AcceptableLandUseCategories { get; set; } = string.Empty;
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
    // Set when a hard-blocker check (flood risk, unresolved liens/debts) caps the score below
    // what the passed/applicable ratio alone would give — explains the gap between "most checks
    // passed" and a low score, instead of leaving it looking inconsistent.
    public string? ScoreCapReason { get; set; }
    public List<ViabilityCheckDto> Checks { get; set; } = new();
    // Named establishments behind the "Competencia directa" check's count — nearest first,
    // capped (see ViabilityAnalysisService.MaxNamedCompetitors). Empty when the business type
    // isn't competitor-sensitive (no CompetitorExclusionRadiusMeters) or none were found.
    public List<NearbyBusinessDto> NearbyCompetitors { get; set; } = new();
}

// A named, real establishment near the point — either a same-category competitor
// (BusinessViabilityDto.NearbyCompetitors) or a traffic generator (ViabilityAnalysisDto.
// TrafficGenerators). Distance is straight-line (GeoFactory.DistanceMeters), not driving distance.
public class NearbyBusinessDto
{
    public string Name { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    // So the frontend can plot a real marker on the viability-report map, not just list the
    // name/distance as text.
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class LatLngDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class NearbyRoadDto
{
    public string? Name { get; set; }
    public string Classification { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    // The road's own shape near this point (OSM way geometry) — lets the frontend draw it
    // highlighted (a dashed line), not just mark a single point on it. Empty when OSM had no
    // geometry for this element (shouldn't happen for a way, but defend against it anyway).
    public List<LatLngDto> Geometry { get; set; } = new();
}

public class TrafficGeneratorDto
{
    public string Name { get; set; } = string.Empty;
    public int DistanceMeters { get; set; }
    // Human label for which search turned this up — "Escuela", "Centro comercial", "Transporte
    // público" (see ViabilityAnalysisService.TrafficGeneratorSearches).
    public string Category { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class ViabilityCheckDto
{
    public string Label { get; set; } = string.Empty;
    // true = meets the criterion, false = fails it, null = the listing is missing the data
    // needed to evaluate this one — ViabilityAnalysisService.ScoreBusinessAsync counts that
    // against the score (a listing with little data filled in shouldn't score near 100% just
    // because the few checks it COULD run happened to pass).
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
    // Nearby anchors (schools, malls, transit) a business would benefit from being close to —
    // shared across every business type, unlike NearbyCompetitors which is per-type. See
    // ViabilityAnalysisService.TrafficGeneratorSearches for what's searched.
    public List<TrafficGeneratorDto> TrafficGenerators { get; set; } = new();
    // Real mapped highways/avenues near the point (OpenStreetMap), not just the listing's own
    // self-reported PrimaryVialidadType — see ViabilityAnalysisService.FindNearbyRoadsAsync.
    public List<NearbyRoadDto> NearbyRoads { get; set; } = new();
    // True when the OSM/Overpass road lookup itself failed (both mirrors down/erroring) rather
    // than genuinely finding nothing — an empty NearbyRoads otherwise reads identically to "no
    // major roads nearby", which is what a 2026-10-18 Overpass outage was mistaken for a bug.
    public bool RoadsDataUnavailable { get; set; }
    // Same distinction for TrafficGenerators: true when at least one of its sources (a DENUE
    // category search or the Overpass transit search) failed, so the list may be incomplete
    // rather than exhaustive.
    public bool TrafficGeneratorsDataUnavailable { get; set; }
}
