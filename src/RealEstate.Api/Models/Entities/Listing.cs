using NetTopologySuite.Geometries;

namespace RealEstate.Api.Models.Entities;

public class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ListingType ListingType { get; set; }
    public PropertyType PropertyType { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    // Lets an owner explicitly publish without a number (commercial/land listings especially
    // often go this route) instead of leaving Price at 0 — ListingsController.Create/Update
    // reject Price <= 0 unless this is set, and the frontend shows "Price on request" instead of
    // a misleading "$0" whenever it's true.
    public bool PriceOnRequest { get; set; }

    // Admin-only override (ListingsController.UpdateOwnerContact) for when the account that
    // actually owns this listing row is an agent representing the real property owner — lets an
    // admin swap the listing-detail contact card from that agent's info to the real owner's own,
    // free-text contact instead (the owner often has no account here at all, which is why this
    // is plain text rather than a link to an Owner/Agent profile). HideAgentContact alone, not a
    // derived "has owner contact info" check, controls which card the frontend shows — an admin
    // can fill these in ahead of time without immediately switching the public-facing card.
    public bool HideAgentContact { get; set; }
    public string? OwnerContactName { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? OwnerContactEmail { get; set; }

    public ListingAddress? Address { get; set; }

    // Incremented by POST /api/listings/{id}/view (ListingsController) — same convention as
    // Agent.ViewCount: raw per-load counter, deduplicated client-side via localStorage rather
    // than server-side.
    public int ViewCount { get; set; }

    // Used for Google Maps pins
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // DB-computed (GENERATED ALWAYS AS ... STORED from Latitude/Longitude — see
    // ApplicationDbContext.OnModelCreating) and GIST-indexed, matching AgebPopulation.Boundary /
    // MunicipalBoundary.Boundary's convention. Never set from app code — Latitude/Longitude stay
    // the source of truth; this exists only so the map's bounding-box search
    // (ListingsController.Search) can use ST_Intersects with a real spatial index instead of a
    // plain B-tree on (Latitude, Longitude), which can't efficiently satisfy a 2D range query.
    public Point? Location { get; set; }

    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public int AreaSqFt { get; set; }
    public int? YearBuilt { get; set; }
    public int? ParkingSpaces { get; set; }
    // Only meaningful when ParkingSpaces is set — whether those spaces are a roofed garage/
    // carport vs. an open-air pad.
    public bool? ParkingIsCovered { get; set; }
    public int? Floors { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public decimal? GardenSizeSqm { get; set; }
    public bool HasHeatingCooling { get; set; }
    public bool HasRoofGarden { get; set; }
    public decimal? HoaFee { get; set; }
    public string? VideoTourUrl { get; set; }
    // Nueva/Usada/Remodelada/EnObra — not meaningful for raw land, same as YearBuilt above.
    public ConstructionConditionType? ConstructionCondition { get; set; }
    public CardinalOrientationType? FacadeOrientation { get; set; }
    public bool? HasNaturalGas { get; set; }
    // "Gas estacionario" in Mexican listings — an LP tank, as opposed to piped natural gas above.
    public bool? HasLpGas { get; set; }
    // A rooftop/underground water storage tank — common even in apartment buildings in Mexico
    // given irregular municipal water pressure/schedules, so (unlike HasWell below) this applies
    // to any property type.
    public bool? HasCistern { get; set; }
    // An on-site well — frontend-gated to hide/disable this for PropertyType.Apartment (not
    // something a single apartment unit would have its own well for), but left ungated here at
    // the entity/DB level, same as every other optional boolean on this model.
    public bool? HasWell { get; set; }

    // Room checkboxes + amenities — frontend-gated to House/CondoHouse/Apartment (see
    // ListingFormComponent.isHouseOrApartment()), same ungated-at-the-entity-level treatment as
    // every other optional boolean above.
    public bool? HasKitchen { get; set; }
    public bool? HasLivingRoom { get; set; }
    public bool? HasDiningRoom { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasTerrace { get; set; }
    public bool? HasBalcony { get; set; }
    public bool? HasServiceRoom { get; set; }
    public bool? HasLaundryArea { get; set; }
    public bool? HasStorageRoom { get; set; }
    public bool? HasGym { get; set; }
    public bool? HasSecurity { get; set; }
    public bool? HasElevator { get; set; }
    public bool? HasBbqArea { get; set; }

    // Land/commercial characteristics — only meaningful when PropertyType is Land, Commercial,
    // ResidentialLand, CommercialLand, or IndustrialLand (see ListingFormComponent's
    // propertyType-gated section). All nullable: never populated for residential/other property
    // types, same treatment as LotSizeSqm/GardenSizeSqm above.

    // Zoning / tenure / buildability
    public string? LandUseZoning { get; set; }              // free-text, jurisdiction-specific zoning code (e.g. "H30-A"), not an enum
    // General land-use classification (Urbano, Urbanizable, No urbanizable, Industrial,
    // Residencial, Comercial, Agrícola) — a fixed catalog (LandUseCategory), distinct from the
    // free-text zoning code above. Only meaningful/settable when PropertyType is Land.
    public int? LandUseCategoryId { get; set; }
    public LandUseCategory? LandUseCategory { get; set; }
    public LandTenureType? LandTenure { get; set; }
    // Whether the seller actually holds the constancia ejidal (the agrarian-registry document
    // proving ejido land rights) — distinct from LandTenure's classification above: a lot can be
    // tenure type Ejidal without the seller having this specific certificate in hand yet.
    public bool? HasEjidalCertificate { get; set; }
    public decimal? CosCoefficient { get; set; }             // Coeficiente de Ocupacion del Suelo
    public decimal? CusCoefficient { get; set; }             // Coeficiente de Utilizacion del Suelo
    public decimal? MaxHeightMeters { get; set; }

    // Liens / debts
    public bool? IsFreeOfLiens { get; set; }
    public bool? HasPropertyTaxDebt { get; set; }            // predial
    public bool? HasWaterDebt { get; set; }

    // Dimensions
    public decimal? FrontageWidthMeters { get; set; }
    public decimal? FrontageDepthMeters { get; set; }

    // Utilities
    public bool? HasPotableWater { get; set; }
    public bool? HasDrainage { get; set; }
    public bool? HasElectricity { get; set; }
    public bool? HasThreePhaseElectricity { get; set; }      // three-phase/high-tension — essential for most commercial/industrial uses
    public bool? HasTelecomService { get; set; }

    // Access / lot geometry
    public bool? HasVehicleAccess { get; set; }
    public bool? HasPavedStreet { get; set; }
    public bool? HasNearbyUTurn { get; set; }
    public bool? IsCornerLot { get; set; }
    public int? StreetFrontageCount { get; set; }
    public VialidadType? PrimaryVialidadType { get; set; }   // simplified single "main road type" classifier
    public LotShapeType? LotShape { get; set; }
    public TopographyType? Topography { get; set; }
    public bool? IsFloodRiskZone { get; set; }

    // Valuation
    public decimal? CadastralValue { get; set; }             // valor catastral

    // Set when this listing is a lot/house inside a published (or still-under-review)
    // Fraccionamiento — see Fraccionamiento.cs for why units aren't a separate entity.
    public Guid? FraccionamientoId { get; set; }
    public Fraccionamiento? Fraccionamiento { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
    public ICollection<ListingPriceHistory> PriceHistory { get; set; } = new List<ListingPriceHistory>();
}
