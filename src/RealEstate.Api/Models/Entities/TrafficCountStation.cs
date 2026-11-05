namespace RealEstate.Api.Models.Entities;

// One SICT federal-highway traffic-count station — real measured TDPA (Tránsito Diario Promedio
// Anual / AADT), imported from a government open-data CSV (see
// database/data-imports/traffic-counts/README.md), not admin-entered or computed. int Id, not
// Guid: this is reference data loaded wholesale from an external source and re-truncated/reloaded
// on every import run (like LandUseCategory/BusinessTypeCriteria), not user-generated content.
//
// Shown in the commercial viability report (ViabilityAnalysisService, via
// TrafficCountStationProximity) as informational data only — never scored as a pass/fail
// criterion. There's no sourced per-business-type "minimum acceptable TDPA" threshold, and
// inventing one would repeat the exact unverified-threshold mistake corrected across
// business_type_criteria on 2026-11-04/05 (see project_business_criteria_research_backlog memory).
public class TrafficCountStation
{
    public int Id { get; set; }

    public string RouteName { get; set; } = string.Empty;
    // Short route code as published, e.g. "MEX-045" — not every station has one.
    public string? RouteKey { get; set; }
    public string State { get; set; } = string.Empty;

    // Tránsito Diario Promedio Anual — average vehicles per day, both directions combined.
    public int Tdpa { get; set; }
    // e.g. "Enero-diciembre 2024" — the measurement window this TDPA covers, shown alongside it so
    // a viewer isn't left assuming it's a live/current count.
    public string Period { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
