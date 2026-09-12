namespace RealEstate.Api.Models.Entities;

public class Agent
{
    public int Id { get; set; }

    // Links to ApplicationUser.Id in the separate ApplicationDbContext (Identity),
    // so no navigation property / EF-enforced FK across contexts.
    public Guid? UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public List<Property> Properties { get; set; } = new();
}
