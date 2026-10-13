namespace RealEstate.Api.Models.Entities;

// A plain property owner's public profile — the Propietarios directory's equivalent of Agent,
// trimmed to what a non-agent owner actually has: no Brokerage/Specialties/Reviews, since those
// are agent-specific concepts. Not auto-derived from ApplicationUser, for the same reason Agent
// isn't: ApplicationUser has no Phone/PhotoUrl/Bio fields of its own.
//
// Created one of two ways: self-service, by a logged-in Owner-role user filling out
// OwnersController.Create (UserId set immediately, "active" from the start) — or admin-initiated,
// auto-created by ListingsController.UpdateOwnerContact the moment an admin sets a listing's
// free-text owner-contact email (UserId left null, i.e. "inactive" — unclaimed, no account of
// its own yet). An inactive row is claimed (UserId set) the moment someone proves they own that
// email — verifying a fresh Register, or signing in via Google/Facebook — and requests the Owner
// role; see AuthController.ClaimOwnerProfileIfMatchingAsync.
public class Owner
{
    public int Id { get; set; }

    // Links to ApplicationUser.Id in the separate ApplicationDbContext (Identity),
    // so no navigation property / EF-enforced FK across contexts — same as Agent.UserId.
    // Null means "inactive" — see this class's own comment above.
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
