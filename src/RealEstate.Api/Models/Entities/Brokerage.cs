namespace RealEstate.Api.Models.Entities;

// A growing catalog of real estate agencies, built up as agents register their company name.
// Powers the autocomplete suggestions on the agent signup form.
public class Brokerage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Public agency profile fields — all optional, filled in later by a member agent via
    // BrokeragesController.Update (the row itself is created with just a Name, resolved from
    // whatever an agent first typed at signup; see AgentsController.ResolveOrCreateBrokerageAsync).
    public string? LogoUrl { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }

    public List<Agent> Agents { get; set; } = new();
}
