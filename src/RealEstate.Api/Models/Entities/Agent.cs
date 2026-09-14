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

    public int? BrokerageId { get; set; }
    public Brokerage? Brokerage { get; set; }

    // Not a mapped column (no setter, so EF Core's default conventions skip it) — the
    // brokerage name is only ever stored once, on Brokerage.Name, and read through here so
    // every existing caller that read Agent.Company keeps working unchanged.
    public string? Company => Brokerage?.Name;

    public bool IsIndependent { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Specialties { get; set; } = new();

    public List<Property> Properties { get; set; } = new();
    public List<AgentReview> Reviews { get; set; } = new();
}
