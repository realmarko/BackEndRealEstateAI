namespace RealEstate.Api.Models.Entities;

public class AgentReview
{
    public int Id { get; set; }

    public int AgentId { get; set; }
    public Agent Agent { get; set; } = null!;

    // Links to ApplicationUser.Id in the separate ApplicationDbContext (Identity),
    // so no navigation property / EF-enforced FK across contexts — same pattern as Agent.UserId.
    public Guid ReviewerUserId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;

    public int Rating { get; set; }
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
