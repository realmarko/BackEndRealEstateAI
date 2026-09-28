using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using RealEstate.Api.Extensions;
using Xunit;

namespace RealEstate.Api.Tests.Extensions;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuthType"));

    [Fact]
    public void GetUserId_ParsesNameIdentifierClaim()
    {
        var id = Guid.NewGuid();
        var user = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        Assert.Equal(id, user.GetUserId());
    }

    [Fact]
    public void GetUserId_FallsBackToSubClaimWhenNameIdentifierMissing()
    {
        var id = Guid.NewGuid();
        var user = BuildPrincipal(new Claim("sub", id.ToString()));

        Assert.Equal(id, user.GetUserId());
    }

    [Fact]
    public void GetUserId_ThrowsWhenNoIdClaimIsPresent()
    {
        var user = BuildPrincipal();

        Assert.Throws<ArgumentNullException>(() => user.GetUserId());
    }

    [Fact]
    public void TryGetUserId_ReturnsIdWhenNameIdentifierClaimIsPresent()
    {
        var id = Guid.NewGuid();
        var user = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, id.ToString()));

        Assert.Equal(id, user.TryGetUserId());
    }

    [Fact]
    public void TryGetUserId_ReturnsNullForAnonymousUser()
    {
        var user = BuildPrincipal();

        Assert.Null(user.TryGetUserId());
    }

    [Fact]
    public void TryGetUserId_ReturnsNullWhenClaimIsMalformed()
    {
        var user = BuildPrincipal(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));

        Assert.Null(user.TryGetUserId());
    }

    [Fact]
    public void TryGetEmail_ReturnsJwtEmailClaimWhenPresent()
    {
        var user = BuildPrincipal(new Claim(JwtRegisteredClaimNames.Email, "jwt@example.com"));

        Assert.Equal("jwt@example.com", user.TryGetEmail());
    }

    [Fact]
    public void TryGetEmail_FallsBackToClaimTypesEmail()
    {
        var user = BuildPrincipal(new Claim(ClaimTypes.Email, "mapped@example.com"));

        Assert.Equal("mapped@example.com", user.TryGetEmail());
    }

    [Fact]
    public void TryGetEmail_ReturnsNullForAnonymousUser()
    {
        var user = BuildPrincipal();

        Assert.Null(user.TryGetEmail());
    }
}
