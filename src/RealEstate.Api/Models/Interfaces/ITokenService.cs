using RealEstate.Api.Models.Entities;
namespace RealEstate.Api.Models.Interfaces;
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(ApplicationUser user, IList<string> roles);
}