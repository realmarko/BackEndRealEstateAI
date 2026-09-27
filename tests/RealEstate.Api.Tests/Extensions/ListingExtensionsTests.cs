using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Tests.Extensions;

public class ListingExtensionsTests
{
    private static Listing MakeListing() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Casa en venta",
        Description = "Una casa",
        ListingType = ListingType.Sale,
        PropertyType = PropertyType.House,
        Status = ListingStatus.Active,
        Price = 1_500_000m,
        Currency = "MXN",
        Latitude = 19.04,
        Longitude = -98.20,
        OwnerId = Guid.NewGuid()
    };

    [Fact]
    public void ToDto_WithAddress_MapsAllAddressFields()
    {
        var listing = MakeListing();
        listing.Address = new ListingAddress
        {
            ListingId = listing.Id,
            Street = "Calle Reforma 123",
            Colonia = "Centro",
            City = "Puebla",
            State = "Puebla",
            ZipCode = "72000",
            Country = "México"
        };

        var dto = listing.ToDto();

        Assert.Equal("Calle Reforma 123", dto.Street);
        Assert.Equal("Centro", dto.Colonia);
        Assert.Equal("Puebla", dto.City);
        Assert.Equal("Puebla", dto.State);
        Assert.Equal("72000", dto.ZipCode);
        Assert.Equal("México", dto.Country);
    }

    [Fact]
    public void ToDto_WithoutAddress_FallsBackToEmptyStrings()
    {
        // A Listing whose Address navigation wasn't Include()-d (or a decommissioned row that
        // predates the 1:1 address table) must never null-ref the mapper.
        var listing = MakeListing();
        listing.Address = null;

        var dto = listing.ToDto();

        Assert.Equal(string.Empty, dto.Street);
        Assert.Equal(string.Empty, dto.Colonia);
        Assert.Equal(string.Empty, dto.City);
        Assert.Equal(string.Empty, dto.State);
        Assert.Equal(string.Empty, dto.ZipCode);
        Assert.Equal(string.Empty, dto.Country);
    }

    [Fact]
    public void ToDto_WithOwner_JoinsFirstAndLastName()
    {
        var listing = MakeListing();
        listing.Owner = new ApplicationUser
        {
            FirstName = "Marco",
            LastName = "Martinez",
            Email = "marco@example.com",
            UserName = "marco@example.com"
        };

        var dto = listing.ToDto();

        Assert.Equal("Marco Martinez", dto.OwnerName);
    }

    [Fact]
    public void ToDto_WithoutOwner_OwnerNameIsEmpty()
    {
        var listing = MakeListing();
        listing.Owner = null;

        var dto = listing.ToDto();

        Assert.Equal(string.Empty, dto.OwnerName);
    }

    [Fact]
    public void ToDto_OrdersImagesBySortOrder_RegardlessOfCollectionOrder()
    {
        var listing = MakeListing();
        listing.Images =
        [
            new ListingImage { Url = "third.jpg", SortOrder = 2 },
            new ListingImage { Url = "first.jpg", SortOrder = 0 },
            new ListingImage { Url = "second.jpg", SortOrder = 1 }
        ];

        var dto = listing.ToDto();

        Assert.Equal(["first.jpg", "second.jpg", "third.jpg"], dto.ImageUrls);
    }

    [Fact]
    public void ToDto_MapsEnumsToTheirStringNames()
    {
        var listing = MakeListing();
        listing.ListingType = ListingType.Rent;
        listing.PropertyType = PropertyType.Apartment;
        listing.Status = ListingStatus.Pending;

        var dto = listing.ToDto();

        Assert.Equal("Rent", dto.ListingType);
        Assert.Equal("Apartment", dto.PropertyType);
        Assert.Equal("Pending", dto.Status);
    }
}
