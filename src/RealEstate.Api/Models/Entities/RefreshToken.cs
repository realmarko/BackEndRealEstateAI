namespace RealEstate.Api.Models.Entities;

// Long-lived half of the access/refresh pair (see RefreshTokenService) — the raw token is never
// stored, only its hash, same as a password. RevokedAt/ReplacedByTokenId implement rotation: a
// token that's already been redeemed (ReplacedByTokenId set) but gets presented again is a reuse/
// theft signal, not just an expired-token error.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
