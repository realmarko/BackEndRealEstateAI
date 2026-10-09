using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RealEstate.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Parses the authenticated user's id from the "sub"/NameIdentifier claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")!);

    /// <summary>Same as <see cref="GetUserId"/>, but returns null instead of throwing when the
    /// caller is anonymous or the claim is missing/malformed — for endpoints anyone can hit.</summary>
    public static Guid? TryGetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>The authenticated user's email, or null for an anonymous caller. Checks both claim
    /// spellings since whether the JWT handler remaps "email" to ClaimTypes.Email depends on
    /// MapInboundClaims configuration — TokenService issues the JWT-standard name.</summary>
    public static string? TryGetEmail(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? user.FindFirstValue(ClaimTypes.Email);

    /// <summary>True when the caller sent a valid bearer token — works even on an endpoint with no
    /// [Authorize] attribute, since UseAuthentication() always tries to populate the request's
    /// ClaimsPrincipal regardless of whether the action requires it.</summary>
    public static bool IsAuthenticated(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true;
}
