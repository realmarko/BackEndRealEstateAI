using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface ITokenService
{
    (string token, DateTime expiresAt) CreateToken(ApplicationUser user, IList<string> roles);
}
