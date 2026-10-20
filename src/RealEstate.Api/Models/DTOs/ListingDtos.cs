using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

public class ListingCreateDto
{
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(5000)] public string Description { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public PropertyType PropertyType { get; set; }
    public decimal Price { get; set; }
    public bool PriceOnRequest { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "MXN";
    [MaxLength(300)] public string Street { get; set; } = string.Empty;

    // Nullable, like ZipCode below: [FromForm] binding's implicit-required check for
    // non-nullable reference types rejects an empty string, not just a missing key — and every
    // listing that existed before this field split genuinely has no colonia (it didn't exist as
    // a concept yet), so the frontend form leaves it optional and sends "" for those.
    [MaxLength(150)] public string? Colonia { get; set; }
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string State { get; set; } = string.Empty;

    // Nullable for the same reason as Colonia above — not every listing has a known zip code,
    // and the frontend form leaves this optional too.
    [MaxLength(20)] public string? ZipCode { get; set; }
    [MaxLength(100)] public string Country { get; set; } = "México";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public int AreaSqFt { get; set; }
    public int? YearBuilt { get; set; }
    public int? ParkingSpaces { get; set; }
    public bool? ParkingIsCovered { get; set; }
    public int? Floors { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public decimal? GardenSizeSqm { get; set; }
    public bool HasHeatingCooling { get; set; }
    public bool HasRoofGarden { get; set; }
    public decimal? HoaFee { get; set; }
    [MaxLength(2048)] public string? VideoTourUrl { get; set; }
    public ConstructionConditionType? ConstructionCondition { get; set; }
    public CardinalOrientationType? FacadeOrientation { get; set; }
    public bool? HasNaturalGas { get; set; }
    public bool? HasLpGas { get; set; }
    public bool? HasCistern { get; set; }
    public bool? HasWell { get; set; }

    [MaxLength(50)] public string? LandUseZoning { get; set; }
    public int? LandUseCategoryId { get; set; }
    public LandTenureType? LandTenure { get; set; }
    public bool? HasEjidalCertificate { get; set; }
    // Generous, sanity-check bounds (not precise Mexican zoning limits, which vary by
    // municipality) — just enough to reject obvious data-entry errors like a misplaced decimal
    // point or a negative value, not to enforce a specific jurisdiction's actual rules.
    [Range(0, 10)] public decimal? CosCoefficient { get; set; }
    [Range(0, 50)] public decimal? CusCoefficient { get; set; }
    [Range(0, 1000)] public decimal? MaxHeightMeters { get; set; }
    public bool? IsFreeOfLiens { get; set; }
    public bool? HasPropertyTaxDebt { get; set; }
    public bool? HasWaterDebt { get; set; }
    [Range(0, 10000)] public decimal? FrontageWidthMeters { get; set; }
    [Range(0, 10000)] public decimal? FrontageDepthMeters { get; set; }
    public bool? HasPotableWater { get; set; }
    public bool? HasDrainage { get; set; }
    public bool? HasElectricity { get; set; }
    public bool? HasThreePhaseElectricity { get; set; }
    public bool? HasTelecomService { get; set; }
    public bool? HasVehicleAccess { get; set; }
    public bool? HasPavedStreet { get; set; }
    public bool? HasNearbyUTurn { get; set; }
    public bool? IsCornerLot { get; set; }
    [Range(0, 20)] public int? StreetFrontageCount { get; set; }
    public VialidadType? PrimaryVialidadType { get; set; }
    public LotShapeType? LotShape { get; set; }
    public TopographyType? Topography { get; set; }
    public bool? IsFloodRiskZone { get; set; }
    [Range(0, double.MaxValue)] public decimal? CadastralValue { get; set; }

    // Photos already hosted somewhere — a pasted external link, or an S3 URL kept from a
    // previous edit — sent through as-is, in order, before any newly uploaded photo.
    public List<string>? ExistingImageUrls { get; set; }

    // New photos to upload to S3 (see AgentsController.TryUploadPhotoAsync for the same
    // pattern) — appended after ExistingImageUrls, in the order given.
    public List<IFormFile>? Photos { get; set; }
}

public class ListingUpdateDto : ListingCreateDto
{
    public ListingStatus Status { get; set; }
}

public class ListingTransferDto
{
    public int AgentId { get; set; }
}

public class ListingLocationUpdateDto
{
    // The map's "editar ubicación" banner now accepts typed-in coordinates, not just a
    // physically-constrained drag (which can never leave valid lat/lng ranges) — these bounds
    // reject an obviously wrong value (e.g. a swapped lat/lng, or a stray extra digit) instead
    // of silently saving a pin nowhere near any real map position.
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
}

public class ListingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool PriceOnRequest { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Street { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool HideAgentContact { get; set; }
    public string? OwnerContactName { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? OwnerContactEmail { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Bedrooms { get; set; }
    public decimal Bathrooms { get; set; }
    public int AreaSqFt { get; set; }
    public int? YearBuilt { get; set; }
    public int? ParkingSpaces { get; set; }
    public bool? ParkingIsCovered { get; set; }
    public int? Floors { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public decimal? GardenSizeSqm { get; set; }
    public bool HasHeatingCooling { get; set; }
    public bool HasRoofGarden { get; set; }
    public decimal? HoaFee { get; set; }
    public string? VideoTourUrl { get; set; }
    public string? ConstructionCondition { get; set; }
    public string? FacadeOrientation { get; set; }
    public bool? HasNaturalGas { get; set; }
    public bool? HasLpGas { get; set; }
    public bool? HasCistern { get; set; }
    public bool? HasWell { get; set; }
    public string? LandUseZoning { get; set; }
    public int? LandUseCategoryId { get; set; }
    public string? LandTenure { get; set; }
    public bool? HasEjidalCertificate { get; set; }
    public decimal? CosCoefficient { get; set; }
    public decimal? CusCoefficient { get; set; }
    public decimal? MaxHeightMeters { get; set; }
    public bool? IsFreeOfLiens { get; set; }
    public bool? HasPropertyTaxDebt { get; set; }
    public bool? HasWaterDebt { get; set; }
    public decimal? FrontageWidthMeters { get; set; }
    public decimal? FrontageDepthMeters { get; set; }
    public bool? HasPotableWater { get; set; }
    public bool? HasDrainage { get; set; }
    public bool? HasElectricity { get; set; }
    public bool? HasThreePhaseElectricity { get; set; }
    public bool? HasTelecomService { get; set; }
    public bool? HasVehicleAccess { get; set; }
    public bool? HasPavedStreet { get; set; }
    public bool? HasNearbyUTurn { get; set; }
    public bool? IsCornerLot { get; set; }
    public int? StreetFrontageCount { get; set; }
    public string? PrimaryVialidadType { get; set; }
    public string? LotShape { get; set; }
    public string? Topography { get; set; }
    public bool? IsFloodRiskZone { get; set; }
    public decimal? CadastralValue { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;

    // Neither is a Listing field — the owner isn't necessarily even an Agent (could be an
    // Owner-role user). Populated after the fact by matching OwnerId to an Agent.UserId (see
    // ListingsController.AttachOwnerAgentDetailsAsync); null when there's no matching agent, or
    // the matching agent has no company/photo set.
    public string? OwnerCompany { get; set; }
    public string? OwnerPhotoUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public int ViewCount { get; set; }
}

public class ListingPriceHistoryDto
{
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTime RecordedAt { get; set; }
}

public class LatLngPointDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

// A CMA (comparative market analysis) request — the area an agent drew on the map, plus an
// optional property-type narrowing so a house isn't averaged in against a commercial lot.
public class CmaRequestDto
{
    public List<LatLngPointDto> Points { get; set; } = new();
    public PropertyType? PropertyType { get; set; }
}

public class CmaStatsDto
{
    public int Count { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    // Null when none of the matching listings have a usable AreaSqFt to divide by — distinct
    // from Count == 0, which ComposeStatBand-style callers would otherwise conflate.
    public decimal? AvgPricePerSqFt { get; set; }
    public decimal? MedianPricePerSqFt { get; set; }
    public double? AvgDaysOnMarket { get; set; }
}

public class CmaResultDto
{
    public CmaStatsDto Sale { get; set; } = new();
    public CmaStatsDto Rent { get; set; } = new();
    // Capped, newest first — see ListingsController.Cma for the exact limit and reasoning.
    public List<ListingDto> Comparables { get; set; } = new();
}

// Admin-only (ListingsController.UpdateOwnerContact) — lets an admin swap a listing's public
// contact card from the agent who posted it to the real property owner's own free-text info.
public class UpdateListingOwnerContactDto
{
    public bool HideAgentContact { get; set; }
    [MaxLength(200)] public string? OwnerContactName { get; set; }
    [MaxLength(30)] public string? OwnerContactPhone { get; set; }
    [EmailAddress, MaxLength(320)] public string? OwnerContactEmail { get; set; }
}

public class ListingSearchQuery
{
    public string? City { get; set; }
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }

    // Scopes results to listings owned by an agent who belongs to this brokerage (an
    // "inmobiliaria" page) — see ListingsController.Search for the cross-context resolution.
    public int? BrokerageId { get; set; }

    // Optional Google Maps viewport bounding box, used when the user pans/zooms the map
    public double? SwLat { get; set; }
    public double? SwLng { get; set; }
    public double? NeLat { get; set; }
    public double? NeLng { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class LandUseCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ListingQuotaDto
{
    public int Limit { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetsAt { get; set; }
}
