using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Extensions;

public static class ListingExtensions
{
    /// <summary>Maps a Listing entity (with its Images and Owner loaded) to the API's
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
        AddressLine = l.AddressLine,
        City = l.City,
        State = l.State,
        ZipCode = l.ZipCode,
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        Bedrooms = l.Bedrooms,
        Bathrooms = l.Bathrooms,
        AreaSqFt = l.AreaSqFt,
        YearBuilt = l.YearBuilt,
        OwnerId = l.OwnerId,
        OwnerName = l.Owner is null ? string.Empty : $"{l.Owner.FirstName} {l.Owner.LastName}",
        CreatedAt = l.CreatedAt,
        ImageUrls = l.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList()
    };
}
