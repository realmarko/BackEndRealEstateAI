using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealEstate.Api.Data;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _options;

    public RefreshTokenService(ApplicationDbContext db, IOptions<JwtOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<(string rawToken, RefreshToken entity)> CreateAsync(Guid userId, string? ip)
    {
        // 256 bits of randomness, base64url (no padding) so it's safe in a JSON body/URL without
        // escaping — this is the value the client stores and sends back; only its hash is ever
        // persisted here.
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ip
        };

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();

        return (rawToken, entity);
    }

    public async Task<RefreshToken?> FindValidAsync(string rawToken)
    {
        var token = await FindAnyAsync(rawToken);
        return token is not null && token.RevokedAt is null && token.ExpiresAt > DateTime.UtcNow
            ? token
            : null;
    }

    public Task<RefreshToken?> FindAnyAsync(string rawToken) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == Hash(rawToken));

    public async Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId = null)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByTokenId = replacedByTokenId;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> TryClaimAsync(RefreshToken token)
    {
        // A conditional UPDATE, not a read-then-write — closes the race where two requests
        // presenting the same still-valid token (e.g. two browser tabs both refreshing around
        // the same moment) could otherwise both pass an in-memory "not revoked yet" check and
        // each mint their own child token from it.
        var claimed = await _db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        return claimed == 1;
    }

    public Task LinkReplacementAsync(Guid tokenId, Guid replacedByTokenId) =>
        _db.RefreshTokens
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByTokenId, replacedByTokenId));

    public Task RevokeAllForUserAsync(Guid userId) =>
        _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

    private static string Hash(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
}
