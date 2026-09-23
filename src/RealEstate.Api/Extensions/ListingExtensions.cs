using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Extensions;

public static class ListingExtensions
{
    /// <summary>Maps a Listing entity (with its Images, Owner, and Address loaded) to the API's
    /// ListingDto shape. Shared by ListingsController and AgentsController so both return
    /// listings in exactly the same shape.</summary>
    public static ListingDto ToDto(this Listing l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        ListingType = l.ListingType.ToString(),
        PropertyType = l.PropertyType.ToString(),
        Status = l.Status.ToString(),
        Price = l.Price,
        Currency = l.Currency,
        Street = l.Address?.Street ?? string.Empty,
        Colonia = l.Address?.Colonia ?? string.Empty,
        City = l.Address?.City ?? string.Empty,
        State = l.Address?.State ?? string.Empty,
        ZipCode = l.Address?.ZipCode ?? string.Empty,
        Country = l.Address?.Country ?? string.Empty,
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        Bedrooms = l.Bedrooms,
        Bathrooms = l.Bathrooms,
        AreaSqFt = l.AreaSqFt,
        YearBuilt = l.YearBuilt,
        ParkingSpaces = l.ParkingSpaces,
        Floors = l.Floors,
        LotSizeSqm = l.LotSizeSqm,
        GardenSizeSqm = l.GardenSizeSqm,
        HasHeatingCooling = l.HasHeatingCooling,
        HoaFee = l.HoaFee,
        VideoTourUrl = l.VideoTourUrl,
        LandUseZoning = l.LandUseZoning,
        LandTenure = l.LandTenure?.ToString(),
        CosCoefficient = l.CosCoefficient,
        CusCoefficient = l.CusCoefficient,
        MaxHeightMeters = l.MaxHeightMeters,
        IsFreeOfLiens = l.IsFreeOfLiens,
        HasPropertyTaxDebt = l.HasPropertyTaxDebt,
        HasWaterDebt = l.HasWaterDebt,
        FrontageWidthMeters = l.FrontageWidthMeters,
        FrontageDepthMeters = l.FrontageDepthMeters,
        HasPotableWater = l.HasPotableWater,
        HasDrainage = l.HasDrainage,
        HasElectricity = l.HasElectricity,
        HasThreePhaseElectricity = l.HasThreePhaseElectricity,
        HasTelecomService = l.HasTelecomService,
        HasVehicleAccess = l.HasVehicleAccess,
        HasNearbyUTurn = l.HasNearbyUTurn,
        IsCornerLot = l.IsCornerLot,
        StreetFrontageCount = l.StreetFrontageCount,
        PrimaryVialidadType = l.PrimaryVialidadType?.ToString(),
        LotShape = l.LotShape?.ToString(),
        Topography = l.Topography?.ToString(),
        IsFloodRiskZone = l.IsFloodRiskZone,
        CadastralValue = l.CadastralValue,
        OwnerId = l.OwnerId,
        OwnerName = l.Owner is null ? string.Empty : $"{l.Owner.FirstName} {l.Owner.LastName}",
        CreatedAt = l.CreatedAt,
        ImageUrls = l.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList()
    };
}
