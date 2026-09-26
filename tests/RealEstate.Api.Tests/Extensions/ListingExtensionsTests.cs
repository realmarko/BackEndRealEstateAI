using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;
using Xunit;

namespace RealEstate.Api.Tests.Extensions;

public class ListingExtensionsTests
{
    private static Listing BuildListing() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Casa en venta",
        Description = "Bonita casa",
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.House,
        Status = ListingStatus.Active,
        Price = 1_500_000m,
        Currency = "MXN",
        AddressLine = "Calle Falsa 123",
        City = "Puebla",
        State = "Puebla",
        ZipCode = "72000",
        Latitude = 19.0413,
        Longitude = -98.2062,
        Bedrooms = 3,
        Bathrooms = 2,
        AreaSqFt = 1200,
        LandTenure = LandTenureType.Privado,
        PrimaryVialidadType = VialidadType.AvenidaPrincipal,
        LotShape = LotShapeType.Regular,
        Topography = TopographyType.Plana,
        Images = new List<ListingImage>
        {
            new() { Url = "https://example.com/2.jpg", SortOrder = 2 },
            new() { Url = "https://example.com/1.jpg", SortOrder = 1 },
            new() { Url = "https://example.com/0.jpg", SortOrder = 0 },
        }
    };

    [Fact]
    public void ToDto_MapsScalarAndEnumFields()
    {
        var listing = BuildListing();

        var dto = listing.ToDto();

        Assert.Equal(listing.Id, dto.Id);
        Assert.Equal(listing.Title, dto.Title);
        Assert.Equal("Sale", dto.ListingType);
        Assert.Equal("House", dto.PropertyType);
        Assert.Equal("Active", dto.Status);
        Assert.Equal(listing.Price, dto.Price);
        Assert.Equal(listing.Latitude, dto.Latitude);
        Assert.Equal(listing.Longitude, dto.Longitude);
    }

    [Fact]
    public void ToDto_MapsNullableEnumsToTheirStringValue()
    {
        var listing = BuildListing();

        var dto = listing.ToDto();

        Assert.Equal("Privado", dto.LandTenure);
        Assert.Equal("AvenidaPrincipal", dto.PrimaryVialidadType);
        Assert.Equal("Regular", dto.LotShape);
        Assert.Equal("Plana", dto.Topography);
    }

    [Fact]
    public void ToDto_LeavesNullableEnumsNullWhenNotSet()
    {
        var listing = BuildListing();
        listing.LandTenure = null;
        listing.PrimaryVialidadType = null;
        listing.LotShape = null;
        listing.Topography = null;

        var dto = listing.ToDto();

        Assert.Null(dto.LandTenure);
        Assert.Null(dto.PrimaryVialidadType);
        Assert.Null(dto.LotShape);
        Assert.Null(dto.Topography);
    }

    [Fact]
    public void ToDto_OrdersImageUrlsBySortOrder()
    {
        var listing = BuildListing();

        var dto = listing.ToDto();

        Assert.Equal(
            new[] { "https://example.com/0.jpg", "https://example.com/1.jpg", "https://example.com/2.jpg" },
            dto.ImageUrls);
    }

    [Fact]
    public void ToDto_ReturnsEmptyOwnerNameWhenOwnerIsNotLoaded()
    {
        var listing = BuildListing();
        listing.Owner = null;

        var dto = listing.ToDto();

        Assert.Equal(string.Empty, dto.OwnerName);
    }

    [Fact]
    public void ToDto_CombinesOwnerFirstAndLastNameWhenOwnerIsLoaded()
    {
        var listing = BuildListing();
        listing.Owner = new ApplicationUser { FirstName = "Ana", LastName = "Garcia", UserName = "ana@example.com" };

        var dto = listing.ToDto();

        Assert.Equal("Ana Garcia", dto.OwnerName);
    }
}
