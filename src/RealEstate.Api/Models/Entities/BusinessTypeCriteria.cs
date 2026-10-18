namespace RealEstate.Api.Models.Entities;

// Site-selection criteria for one kind of business (gasolinera, farmacia, OXXO, ...), used by
// ViabilityAnalysisService to score how well a Land listing fits each business type and explain
// why. Seeded with an initial, defensible-but-provisional set of numbers (see
// AddBusinessTypeCriteriaCatalog migration) — unlike LandUseCategory this IS admin-editable at
// runtime (BusinessTypeCriteriaController PUT), since the user expects these thresholds to be
// tuned over time as real outcomes come in, not fixed at launch.
public class BusinessTypeCriteria
{
    public int Id { get; set; }

    // Stable slug used to match a DENUE/Places search term and as the API/frontend key —
    // "gas_station", "pharmacy", "convenience_store", etc. Never shown to a user directly.
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    // DENUE "Buscar" search term for this category (e.g. "gasolinera", "farmacia") — see
    // DenueService.CountNearbyAsync.
    public string DenueSearchTerm { get; set; } = string.Empty;

    // Google Places "type" fallback when DENUE is unavailable/fails — see the opportunity-circle
    // tool's existing Places fallback pattern in map-view.component.ts.
    public string? GooglePlacesType { get; set; }

    public double MinFrontageMeters { get; set; }
    public double MinLotSizeSqm { get; set; }

    // null = no preference either way; true = prefers a corner lot; false = actively avoids one
    // (e.g. a school prefers a quieter, non-corner, non-primary-road frontage).
    public bool? PrefersCornerLot { get; set; }

    public bool PrefersPrimaryRoadAccess { get; set; }

    // Catchment used for the population-density check — null means this business type weighs
    // traffic/access over population (e.g. a convenience store or gas station).
    public double? PopulationRadiusKm { get; set; }
    public int? MinPopulationInRadius { get; set; }

    // How close an existing same-category competitor can be before it meaningfully hurts
    // viability — null means this business type isn't modeled as competitor-sensitive here.
    public int? CompetitorExclusionRadiusMeters { get; set; }

    public string Notes { get; set; } = string.Empty;
}
