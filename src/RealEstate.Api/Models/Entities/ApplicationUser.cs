using Microsoft.AspNetCore.Identity;

namespace RealEstate.Api.Models.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Registration isn't complete until this code (emailed at registration/resend time) is
    // confirmed via AuthController.VerifyEmail, which flips Identity's own EmailConfirmed flag.
    // Cleared back to null once confirmed — a leftover code should never still validate after
    // that point.
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationCodeExpiresAt { get; set; }

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<SavedSearch> SavedSearches { get; set; } = new List<SavedSearch>();
}
