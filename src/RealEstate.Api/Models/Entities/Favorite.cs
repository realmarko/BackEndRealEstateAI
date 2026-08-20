namespace RealEstate.Api.Models.Entities;

public class Favorite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
