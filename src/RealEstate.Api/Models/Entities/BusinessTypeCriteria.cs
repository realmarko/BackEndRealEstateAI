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

    // null = no stated preference (most of the catalog); true = benefits from a busy road
    // (more exposure/walk-in traffic, e.g. a gas station or convenience store); false = actively
    // wants a quiet road (e.g. a school or residential development, where heavy traffic is a
    // safety/noise problem, not an asset). Scored against the real-time congestion level Google
    // Routes API reports for the nearest major road — see ViabilityAnalysisService's traffic check.
    public bool? PrefersHighTraffic { get; set; }

    // Comma-separated LandUseCategory.Name values this business type can legally operate on
    // (e.g. "Urbano,Comercial") — checked against the listing's own LandUseCategory, not an
    // external lookup, so (unlike the DENUE/population checks) a missing value is never a
    // transient/external failure — it just means ScoreBusinessAsync reports this check as
    // unevaluated instead of skipping it silently (see its own "No se pudo evaluar" messages).
    public string AcceptableLandUseCategories { get; set; } = string.Empty;

    // Catchment used for the population-density check — null means this business type weighs
    // traffic/access over population (e.g. a convenience store or gas station).
    public double? PopulationRadiusKm { get; set; }
    public int? MinPopulationInRadius { get; set; }

    // How close an existing same-category competitor can be before it meaningfully hurts
    // viability — null means this business type isn't modeled as competitor-sensitive here.
    public int? CompetitorExclusionRadiusMeters { get; set; }

    // Minimum CUS (coeficiente de utilización de suelo) this business type needs to pencil out —
    // null means density isn't a factor for this type (most of the catalog: a single-story
    // storefront doesn't care how many floors zoning allows). Set for density-driven types like
    // "Edificio de Departamentos", where buildable area (LotSizeSqm × CUS) is the real constraint,
    // not just raw lot size.
    public double? MinCusRatio { get; set; }

    // True for a business type whose own customers are the people living nearby (retail, food,
    // personal-care, healthcare, education, banking) — a new housing development going up close
    // by is a leading signal of exactly the resident base this type depends on, so
    // ViabilityAnalysisService adds a bonus check when one's found nearby (see its own "Nuevo
    // desarrollo residencial cercano" check). False for a housing-supply type itself
    // (Fraccionamiento Residencial, Casa Unifamiliar, Casa de Interés Social, Edificio de
    // Departamentos — a nearby development is a competing supply, not a demand signal for
    // building another one) and for types this catalog already models as traffic/corridor-driven
    // rather than resident-driven (Gasolinera, Bodega/nave industrial, Oficina — see each row's
    // own Notes for why).
    public bool BenefitsFromNearbyResidentialGrowth { get; set; }

    public string Notes { get; set; } = string.Empty;
}
