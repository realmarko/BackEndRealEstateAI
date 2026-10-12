namespace RealEstate.Api.Models.Entities;

// A plain property owner's public profile — the Propietarios directory's equivalent of Agent,
// trimmed to what a non-agent owner actually has: no Brokerage/Specialties/Reviews, since those
// are agent-specific concepts. Deliberately a separate opt-in row (created via OwnersController.
// Create), not auto-derived from ApplicationUser, for the same reason Agent is: ApplicationUser
// has no Phone/PhotoUrl/Bio fields, and a profile shouldn't appear in the public directory until
// its owner has actually filled it in.
public class Owner
{
    public int Id { get; set; }

    // Links to ApplicationUser.Id in the separate ApplicationDbContext (Identity),
    // so no navigation property / EF-enforced FK across contexts — same as Agent.UserId.
    public Guid? UserId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }

    // Incremented by POST /api/owners/{id}/view (OwnersController) — same convention as
    // Agent.ViewCount (see its own comment): approximates distinct visits via the frontend's
    // localStorage dedup, not a literal request count.
    public int ViewCount { get; set; }

    // Admin-only soft delete (see OwnersController.Delete) — same reasoning as Agent.IsDeleted:
    // keeps the profile's listings history and any inquiries that reference it intact.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
