using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

public class ListingCreateDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ListingType ListingType { get; set; }
    public PropertyType PropertyType { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;

    // Nullable, unlike the other address fields: not yet collected from the form, so it's
    // always sent as an empty string. [FromForm] binding treats an empty string as "no value
    // supplied", which trips the implicit-required check ASP.NET Core adds for non-nullable
    // reference types — making this nullable avoids that false validation failure.
    public string? ZipCode { get; set; }
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

    public string? LandUseZoning { get; set; }
    public LandTenureType? LandTenure { get; set; }
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

public class ListingDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
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
    public string? LandUseZoning { get; set; }
    public string? LandTenure { get; set; }
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

    // Not a Listing field — the owner isn't necessarily even an Agent (could be an Owner-role
    // user). Populated after the fact by matching OwnerId to an Agent.UserId (see
    // ListingsController.AttachOwnerCompaniesAsync); null when there's no matching agent, or
    // the matching agent has no company set.
    public string? OwnerCompany { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
}

public class ListingPriceHistoryDto
{
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTime RecordedAt { get; set; }
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
