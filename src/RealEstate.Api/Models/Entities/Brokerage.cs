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

    // Set only when an Admin creates the row directly from the map (BrokeragesController.Create)
    // to pin a physical office — a row resolved from an agent's signup (the common case, see the
    // comment above) never gets these, so most Brokerage rows have them null. Latitude/Longitude
    // null together means "not shown on the map"; BrokeragesController.Map filters on that.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    // Free-text (e.g. "Lun-Vie 9:00-18:00") rather than structured per-day hours — every other
    // contact field on this entity is free text too, and a brokerage's hours are display-only
    // here, never used in a query (unlike, say, Listing's enum fields).
    public string? WorkingHours { get; set; }

    public List<Agent> Agents { get; set; } = new();
}
