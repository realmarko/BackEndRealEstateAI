using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class RegisterDto
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(128)] public string Password { get; set; } = string.Empty;

    // "Owner" (can publish listings), "Agent" (Owner + gets an agent directory profile), or "Buyer" (browse, favorite, message)
    public string Role { get; set; } = "Buyer";
}

public class LoginDto
{
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;

    // No MaxLength here on purpose: this validates a login attempt against whatever password
    // an account already has, not a new one — RegisterDto.Password's cap only applies going
    // forward. Nothing enforced a length limit before this change, so an existing account could
    // genuinely have a password longer than any cap added here; rejecting it at model-binding
    // would lock that user out before CheckPasswordAsync ever gets to compare hashes.
    [Required] public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [Required] public string Token { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
}
