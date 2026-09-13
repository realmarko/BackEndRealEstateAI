namespace RealEstate.Api.Models.Entities;

// One row per price a listing has ever been shown at — the first row (RecordedAt ==
// Listing.CreatedAt) is the original list price; later rows are added only when Update
// actually changes the price, never on every edit.
public class ListingPriceHistory
{
    public int Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
