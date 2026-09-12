using System.Security.Claims;

namespace RealEstate.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Parses the authenticated user's id from the "sub"/NameIdentifier claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")!);
}
