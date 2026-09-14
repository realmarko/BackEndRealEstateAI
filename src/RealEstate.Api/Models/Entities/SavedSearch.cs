namespace RealEstate.Api.Models.Entities;

// A user's persisted search criteria. When a new Listing is created, ListingsController checks
// it against every SavedSearch and emails the owning user on a match — see
// ListingsController.NotifySavedSearchesAsync. Deliberately excludes the map's viewport bounding
// box (SwLat/SwLng/NeLat/NeLng): that's tied to wherever the map happened to be panned/zoomed at
// save time, not a stable criterion someone would want alerts against indefinitely.
public class SavedSearch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }
    public string? City { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
