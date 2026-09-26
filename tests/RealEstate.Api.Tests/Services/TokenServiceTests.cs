using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;
using Xunit;

namespace RealEstate.Api.Tests.Services;

public class TokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        SigningKey = "this-is-a-test-signing-key-that-is-long-enough-for-hs256",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpiryMinutes = 30
    };

    private static TokenService BuildService(JwtOptions? options = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? Options));

    private static ApplicationUser BuildUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "agent@example.com",
        FirstName = "Ana",
        LastName = "Garcia"
    };

    [Fact]
    public void CreateToken_ProducesAJwtContainingExpectedClaims()
    {
        var service = BuildService();
        var user = BuildUser();

        var (token, _) = service.CreateToken(user, new List<string> { "Agent" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.FirstName, jwt.Claims.Single(c => c.Type == "firstName").Value);
        Assert.Equal(user.LastName, jwt.Claims.Single(c => c.Type == "lastName").Value);
        Assert.Equal("Agent", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void CreateToken_SetsIssuerAndAudienceFromOptions()
    {
        var service = BuildService();
        var user = BuildUser();

        var (token, _) = service.CreateToken(user, new List<string>());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(Options.Issuer, jwt.Issuer);
        Assert.Contains(Options.Audience, jwt.Audiences);
    }

    [Fact]
    public void CreateToken_AddsARoleClaimForEachRole()
    {
        var service = BuildService();
        var user = BuildUser();

        var (token, _) = service.CreateToken(user, new List<string> { "Agent", "Admin" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Equal(new[] { "Agent", "Admin" }, roles);
    }

    [Fact]
    public void CreateToken_ExpiresAtMatchesConfiguredExpiryMinutes()
    {
        var service = BuildService();
        var user = BuildUser();
        var before = DateTime.UtcNow;

        var (_, expiresAt) = service.CreateToken(user, new List<string>());

        var expectedExpiry = before.AddMinutes(Options.ExpiryMinutes);
        Assert.True(Math.Abs((expiresAt - expectedExpiry).TotalSeconds) < 5);
    }

    [Fact]
    public void CreateToken_UsesEmptyStringWhenUserEmailIsNull()
    {
        var service = BuildService();
        var user = BuildUser();
        user.Email = null;

        var (token, _) = service.CreateToken(user, new List<string>());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(string.Empty, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }
}
