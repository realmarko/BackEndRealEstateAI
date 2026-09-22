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

    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;

    // Used for Google Maps pins
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public int AreaSqFt { get; set; }
    public int? YearBuilt { get; set; }
    public int? ParkingSpaces { get; set; }
    public int? Floors { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public decimal? GardenSizeSqm { get; set; }
    public bool HasHeatingCooling { get; set; }
    public decimal? HoaFee { get; set; }
    public string? VideoTourUrl { get; set; }

    // Land/commercial characteristics — only meaningful when PropertyType is Land, Commercial,
    // ResidentialLand, CommercialLand, or IndustrialLand (see ListingFormComponent's
    // propertyType-gated section). All nullable: never populated for residential/other property
    // types, same treatment as LotSizeSqm/GardenSizeSqm above.

    // Zoning / tenure / buildability
    public string? LandUseZoning { get; set; }              // "uso de suelo" — free text, jurisdiction-specific code (e.g. "H30-A"), not an enum
    public LandTenureType? LandTenure { get; set; }
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
