using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ITokenService _tokenService;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterDto dto)
    {
        // Agents can also publish listings, so they get both roles. This bundles the grant at
        // registration time instead of having [Authorize] accept either role — simpler while
        // Agent and Owner need identical permissions, but every Owner-only endpoint would need
        // updating (currently: ListingsController and InquiriesController) if that ever changes.
        var roles = dto.Role switch
        {
            "Owner" => new[] { "Owner" },
            "Agent" => new[] { "Owner", "Agent" },
            _ => new[] { "Buyer" }
        };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRolesAsync(user, roles);

        return await BuildAuthResponse(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        return await BuildAuthResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var email = User.TryGetEmail() ?? User.Identity?.Name;
        var user = await _userManager.FindByEmailAsync(email!);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(ToUserDto(user, roles));
    }

    private async Task<AuthResponseDto> BuildAuthResponse(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateToken(user, roles);
        return new AuthResponseDto
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = ToUserDto(user, roles)
        };
    }

    private static UserDto ToUserDto(ApplicationUser user, IList<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Roles = roles
    };
}
