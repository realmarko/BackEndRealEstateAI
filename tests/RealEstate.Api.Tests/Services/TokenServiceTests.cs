using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Tests.Services;

public class TokenServiceTests
{
    private static TokenService MakeService(JwtOptions? options = null) => new(
        Options.Create(options ?? new JwtOptions
        {
            SigningKey = "this-is-a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            Issuer = "realestate-tests",
            Audience = "realestate-tests-audience",
            ExpiryMinutes = 60
        }));

    private static ApplicationUser MakeUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "agent@example.com",
        UserName = "agent@example.com",
        FirstName = "Ana",
        LastName = "Lopez"
    };

    [Fact]
    public void CreateToken_IncludesUserClaimsAndRoles()
    {
        var service = MakeService();
        var user = MakeUser();

        var (token, _) = service.CreateToken(user, ["Owner", "Agent"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(user.Id.ToString(), jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Ana", jwt.Claims.First(c => c.Type == "firstName").Value);
        Assert.Equal("Lopez", jwt.Claims.First(c => c.Type == "lastName").Value);
        Assert.Equal(["Owner", "Agent"], jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value));
    }

    [Fact]
    public void CreateToken_SetsIssuerAndAudienceFromOptions()
    {
        var service = MakeService();
        var (token, _) = service.CreateToken(MakeUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("realestate-tests", jwt.Issuer);
        Assert.Equal("realestate-tests-audience", jwt.Audiences.Single());
    }

    [Fact]
    public void CreateToken_ExpiresAtMatchesExpiryMinutesFromOptions()
    {
        var service = MakeService(new JwtOptions
        {
            SigningKey = "this-is-a-test-signing-key-that-is-long-enough-for-hmac-sha256",
            Issuer = "i",
            Audience = "a",
            ExpiryMinutes = 30
        });

        var before = DateTime.UtcNow;
        var (_, expiresAt) = service.CreateToken(MakeUser(), []);
        var after = DateTime.UtcNow;

        Assert.InRange(expiresAt, before.AddMinutes(30), after.AddMinutes(30).AddSeconds(1));
    }

    [Fact]
    public void CreateToken_WithNoRoles_ProducesNoRoleClaims()
    {
        var service = MakeService();
        var (token, _) = service.CreateToken(MakeUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Role);
    }
}
