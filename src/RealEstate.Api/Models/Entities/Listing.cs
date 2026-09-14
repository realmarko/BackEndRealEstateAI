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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<Inquiry> Inquiries { get; set; } = new List<Inquiry>();
    public ICollection<ListingPriceHistory> PriceHistory { get; set; } = new List<ListingPriceHistory>();
}
