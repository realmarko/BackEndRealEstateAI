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

    public List<AgentBrokerage> AgentBrokerages { get; set; } = new();

    // Not a mapped column (no setter, so EF Core's default conventions skip it) — only
    // evaluated in-memory against an already-loaded AgentBrokerages (same requirement as the
    // old Brokerage-nav version of this property: callers must Include it first). Picks the
    // row marked IsPrimary, falling back to the oldest membership if none is (shouldn't
    // happen once AgentsController always keeps exactly one IsPrimary, but a brand-new agent
    // created before that invariant existed could still have none). Every existing caller that
    // read this single-string Company keeps working unchanged; see Companies below for the
    // full list.
    public string? Company => AgentBrokerages
        .OrderByDescending(ab => ab.IsPrimary)
        .ThenBy(ab => ab.JoinedAt)
        .Select(ab => ab.Brokerage?.Name)
        .FirstOrDefault();

    // Every brokerage this agent belongs to, primary first — used where showing just one name
    // (Company above) would be misleading (agent-detail, the signup/edit form).
    public List<string> Companies => AgentBrokerages
        .OrderByDescending(ab => ab.IsPrimary)
        .ThenBy(ab => ab.JoinedAt)
        .Select(ab => ab.Brokerage?.Name ?? string.Empty)
        .Where(name => name.Length > 0)
        .ToList();

    public bool IsIndependent { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Specialties { get; set; } = new();

    // Incremented by POST /api/agents/{id}/view (AgentsController) — every raw page load, not
    // deduplicated server-side. The frontend debounces repeat calls from the same browser via
    // localStorage (see agent-detail.component.ts), so this approximates distinct visits rather
    // than literal request counts, without needing a full analytics/session table.
    public int ViewCount { get; set; }

    // Admin-only soft delete (see AgentsController.Delete) — kept instead of a hard delete so
    // the profile's reviews/listings history and any inquiries that reference it survive. Every
    // read path (Search, GetById, directory listings) filters this out; it never appears in
    // public results once set.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // No Properties navigation here on purpose: the real "how many properties has this agent
    // listed" answer comes from Listing.OwnerId == UserId (see AgentsController.CountListingsAsync)
    // — Property is legacy/unused, and an Agent-side nav to it previously let PropertiesCount
    // silently compute against always-empty data instead.
    public List<AgentReview> Reviews { get; set; } = new();
}
