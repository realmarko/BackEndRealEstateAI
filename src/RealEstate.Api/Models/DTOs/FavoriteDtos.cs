namespace RealEstate.Api.Models.DTOs;

public class FavoriteDto
{
    public Guid Id { get; set; }
    public ListingDto Listing { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
