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
}
