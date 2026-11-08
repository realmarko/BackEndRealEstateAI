using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IRefreshTokenService
{
    Task<(string rawToken, RefreshToken entity)> CreateAsync(Guid userId, string? ip);

    // Null if the token doesn't exist, is expired, or was already revoked (including the
    // already-redeemed-via-rotation case — callers that need to tell "expired" apart from
    // "reused" should check FindAnyAsync instead).
    Task<RefreshToken?> FindValidAsync(string rawToken);

    // Unlike FindValidAsync, returns a revoked/expired token too — used by the refresh endpoint
    // to detect reuse of an already-rotated token (RevokedAt set) versus a token that simply
    // doesn't exist.
    Task<RefreshToken?> FindAnyAsync(string rawToken);

    Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId = null);

    // Atomically revokes `token` only if it's still unrevoked right now, returning whether this
    // call was the one that claimed it. See RefreshTokenService.TryClaimAsync for why this has
    // to be a conditional UPDATE rather than a check-then-write.
    Task<bool> TryClaimAsync(RefreshToken token);

    Task LinkReplacementAsync(Guid tokenId, Guid replacedByTokenId);

    Task RevokeAllForUserAsync(Guid userId);
}
