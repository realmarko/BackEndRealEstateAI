namespace RealEstate.Api.Models.Entities;

// A message a prospective buyer/renter sends to a listing owner
public class Inquiry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public Guid? SenderId { get; set; }
    public ApplicationUser? Sender { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string Message { get; set; } = string.Empty;

    // Buyer-qualification answers, all optional — asked in the contact form so the listing
    // owner/agent can tell a serious, ready-to-buy lead from someone just browsing.
    public FundingMethod? FundingMethod { get; set; }
    public PurchaseTimeline? Timeline { get; set; }
    public bool? HasAgent { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
