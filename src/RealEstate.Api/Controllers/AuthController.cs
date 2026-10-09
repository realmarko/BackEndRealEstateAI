using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
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
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;
    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IFacebookTokenValidator _facebookTokenValidator;
    private readonly string _frontendBaseUrl;
    private readonly string _googleClientId;
    private readonly FacebookOptions _facebookOptions;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ITokenService tokenService,
        IEmailService emailService,
        ILogger<AuthController> logger,
        IGoogleTokenValidator googleTokenValidator,
        IFacebookTokenValidator facebookTokenValidator,
        IOptions<FrontendOptions> frontendOptions,
        IOptions<GoogleOptions> googleOptions,
        IOptions<FacebookOptions> facebookOptions)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
        _googleTokenValidator = googleTokenValidator;
        _facebookTokenValidator = facebookTokenValidator;
        _frontendBaseUrl = frontendOptions.Value.BaseUrl;
        _googleClientId = googleOptions.Value.ClientId;
        _facebookOptions = facebookOptions.Value;
    }

    // Registration length allowed for a code entry field (6 digits).
    private const int VerificationCodeLength = 6;
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponseDto>> Register(RegisterDto dto)
    {
        var roles = RolesFor(dto.Role);

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var code = GenerateVerificationCode();
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            // EmailConfirmed stays at Identity's own default (false) until VerifyEmail succeeds —
            // Login rejects any account that hasn't gotten there yet.
            EmailVerificationCode = code,
            EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime)
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRolesAsync(user, roles);
        await SendVerificationCodeEmailAsync(user, code);

        return Ok(new RegisterResponseDto { Email = dto.Email });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            return Unauthorized(new { message = "Invalid email or password" });

        // A correct password for an unverified account still isn't enough to log in — matches
        // Register's whole point: an account isn't usable until its owner proves they control the
        // inbox. requiresVerification lets the frontend route straight to the verify-email screen
        // instead of just showing a dead-end "invalid credentials" message.
        if (!user.EmailConfirmed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Please verify your email before logging in.",
                requiresVerification = true,
                email = user.Email
            });
        }

        return await BuildAuthResponse(user);
    }

    // Verifies the Google ID token server-side (never trusts a client-supplied email directly),
    // then finds-or-creates the matching account purely by email — the same identity key every
    // other sign-in path here already uses. An email that already has a password-based account
    // just logs into that account (confirming it if it wasn't yet — Google has already proven
    // the user controls the inbox); nothing about that account's existing roles changes. A
    // brand-new email creates an account exactly like Register, except pre-confirmed (no code
    // round-trip needed) and with a random, never-used password (Identity requires one on every
    // user; this account is only ever meant to sign in via Google or a later "forgot password").
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponseDto>> Google(GoogleAuthDto dto)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await _googleTokenValidator.ValidateAsync(dto.IdToken, _googleClientId);
        }
        catch (InvalidJwtException ex)
        {
            // The client only ever sees the generic message below (no need to hand a visitor
            // details about why their token was rejected), but without logging the real reason
            // here, a genuine misconfiguration (wrong audience, expired token, clock skew) is
            // completely invisible to whoever operates this environment.
            _logger.LogWarning(ex, "Rejected a Google sign-in token");
            return Unauthorized(new { message = "Invalid Google sign-in." });
        }
        catch (Exception ex)
        {
            // ValidateAsync fetches Google's signing certs over HTTP under the hood — a transient
            // network blip there isn't the visitor's fault (not an actually-invalid token), so it
            // gets a retryable error instead of surfacing as an unhandled 500 from here.
            _logger.LogError(ex, "Failed to reach Google while validating a sign-in token");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Couldn't verify your Google sign-in right now. Please try again." });
        }

        var user = await _userManager.FindByEmailAsync(payload.Email);
        if (user is not null)
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            return await BuildAuthResponse(user);
        }

        var roles = RolesFor(dto.Role);
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var newUser = new ApplicationUser
        {
            UserName = payload.Email,
            Email = payload.Email,
            FirstName = payload.GivenName ?? string.Empty,
            LastName = payload.FamilyName ?? string.Empty,
            EmailConfirmed = true
        };

        // A random password Identity requires but this account never needs — the user only ever
        // signs in via Google (or sets a real one later through the normal forgot-password flow).
        var result = await _userManager.CreateAsync(newUser, Guid.NewGuid().ToString("N") + "Aa1!");
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRolesAsync(newUser, roles);

        return await BuildAuthResponse(newUser);
    }

    // Same find-or-create-by-email shape as Google above, but verified against Facebook's Graph
    // API instead of a signed JWT (Facebook has no equivalent to Google's ID token). A Facebook
    // account that hasn't granted the "email" permission (or has no email on file) can't be
    // matched to an account here at all — email is this app's identity key for every sign-in
    // path, so there's no safe fallback (e.g. a synthetic id@facebook address) that wouldn't risk
    // colliding with or impersonating a real account later.
    [HttpPost("facebook")]
    public async Task<ActionResult<AuthResponseDto>> Facebook(FacebookAuthDto dto)
    {
        FacebookProfile profile;
        try
        {
            profile = await _facebookTokenValidator.ValidateAsync(
                dto.AccessToken, _facebookOptions.AppId, _facebookOptions.AppSecret);
        }
        catch (FacebookAuthException ex)
        {
            _logger.LogWarning(ex, "Rejected a Facebook sign-in token");
            return Unauthorized(new { message = "Invalid Facebook sign-in." });
        }
        // Same reasoning as Google's catch (Exception ex) below: a timeout/network blip reaching
        // Facebook's Graph API isn't the visitor's fault, so it gets a retryable 502 instead of
        // the 401 above, which would wrongly blame an invalid token.
        catch (FacebookUnavailableException ex)
        {
            _logger.LogError(ex, "Failed to reach Facebook while validating a sign-in token");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Couldn't verify your Facebook sign-in right now. Please try again." });
        }

        if (string.IsNullOrEmpty(profile.Email))
        {
            return BadRequest(new
            {
                message = "Your Facebook account needs to share an email address to sign in."
            });
        }

        var user = await _userManager.FindByEmailAsync(profile.Email);
        if (user is not null)
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            return await BuildAuthResponse(user);
        }

        var roles = RolesFor(dto.Role);
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var newUser = new ApplicationUser
        {
            UserName = profile.Email,
            Email = profile.Email,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(newUser, Guid.NewGuid().ToString("N") + "Aa1!");
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        await _userManager.AddToRolesAsync(newUser, roles);

        return await BuildAuthResponse(newUser);
    }

    // Agents can also publish listings, so they get both roles. This bundles the grant at
    // registration time instead of having [Authorize] accept either role — simpler while Agent
    // and Owner need identical permissions, but every Owner-only endpoint would need updating
    // (currently: ListingsController and InquiriesController) if that ever changes. Shared by
    // Register and Google (a brand-new account from either path gets the same role rules).
    private static string[] RolesFor(string? role) => role switch
    {
        "Owner" => new[] { "Owner" },
        "Agent" => new[] { "Owner", "Agent" },
        _ => new[] { "Buyer" }
    };

    // Rate-limited like forgot-password: a per-IP budget bounds both the email volume this can
    // generate and how many codes a guesser can force a single account through.
    [EnableRateLimiting("email-verification")]
    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponseDto>> VerifyEmail(VerifyEmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        var invalid = BadRequest(new { message = "Invalid or expired code." });
        if (user is null) return invalid;

        // Deliberately NOT a "treat as success" shortcut: minting a token here without checking
        // the code would mean anyone who knows an already-verified user's email — no password, no
        // valid code needed — could call this endpoint and get a working session for that account.
        if (user.EmailConfirmed)
            return BadRequest(new { message = "This account is already verified. Please log in." });

        if (user.EmailVerificationCode is null || user.EmailVerificationCodeExpiresAt is null)
            return invalid;
        if (user.EmailVerificationCodeExpiresAt < DateTime.UtcNow) return invalid;
        // Fixed-time-ish comparison isn't critical here (rate limiting already bounds brute force
        // to a handful of attempts per 15 minutes), so a plain string compare is fine.
        if (user.EmailVerificationCode != dto.Code) return invalid;

        user.EmailConfirmed = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiresAt = null;
        await _userManager.UpdateAsync(user);

        return await BuildAuthResponse(user);
    }

    // Always returns 204 whether or not the email is registered or already verified — same
    // enumeration-safety reasoning as ForgotPassword.
    [EnableRateLimiting("email-verification")]
    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode(ResendVerificationCodeDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is not null && !user.EmailConfirmed)
        {
            var code = GenerateVerificationCode();
            user.EmailVerificationCode = code;
            user.EmailVerificationCodeExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
            await _userManager.UpdateAsync(user);
            await SendVerificationCodeEmailAsync(user, code);
        }

        return NoContent();
    }

    private static string GenerateVerificationCode() =>
        Random.Shared.Next(0, 1_000_000).ToString($"D{VerificationCodeLength}");

    private async Task SendVerificationCodeEmailAsync(ApplicationUser user, string code)
    {
        var subject = "Confirm your Espacial.com.mx account";
        var body =
            $"Your verification code is: {code}\n\n" +
            $"Enter it on Espacial.com.mx to finish creating your account. This code expires in " +
            $"{VerificationCodeLifetime.TotalMinutes:0} minutes.\n\n" +
            "If you didn't request this, you can safely ignore this email.";

        try
        {
            await _emailService.SendAsync(user.Email!, $"{user.FirstName} {user.LastName}", subject, body);
        }
        // Same broad catch as ForgotPassword: a transient SMTP failure here must not turn into an
        // unhandled 500 on an already-created account — the resend endpoint is the recovery path.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification code email to user {UserId}", user.Id);
        }
    }

    // Always returns 204 whether or not the email is registered — the response can't be used to
    // enumerate which accounts exist. If the account exists, generates an Identity password-reset
    // token (single-use, expires per DataProtectionTokenProviderOptions.TokenLifespan — 1 day by
    // default) and emails a link back into the frontend's reset-password page.
    [EnableRateLimiting("forgot-password")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl =
                $"{_frontendBaseUrl.TrimEnd('/')}/reset-password" +
                $"?email={Uri.EscapeDataString(dto.Email)}&token={Uri.EscapeDataString(token)}";

            var subject = "Reset your Espacial.com.mx password";
            var body =
                $"We received a request to reset your Espacial.com.mx password.\n\n" +
                $"Reset it here: {resetUrl}\n\n" +
                $"If you didn't request this, you can safely ignore this email — your password won't change.";

            try
            {
                await _emailService.SendAsync(user.Email!, $"{user.FirstName} {user.LastName}", subject, body);
            }
            catch (Exception ex)
            {
                // Deliberately catches everything, not just the usual SmtpException/FormatException/
                // ArgumentException/InvalidOperationException set (see AgentsController.Contact) —
                // this endpoint's whole point is that its response never differs from the
                // unregistered-email path, so even an exception type nobody anticipated (a transient
                // socket/timeout failure, say) must not escape as an unhandled 500 that only
                // registered emails can trigger.
                _logger.LogError(ex, "Failed to send password-reset email to user {UserId}", user.Id);
            }
        }

        return NoContent();
    }

    // Same per-IP budget as forgot-password — every other anonymous endpoint with a side effect
    // in this codebase is rate-limited, and an unbounded number of attempts against a given
    // (email, token) pair is exactly the shape that budget is meant to prevent.
    [EnableRateLimiting("forgot-password")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return BadRequest(new { message = "Invalid or expired reset link." });

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            // Identity's own token-validation failure is indistinguishable here from a genuinely
            // expired/reused token — both should read as "get a new link," not surface Identity's
            // internal error codes to the caller.
            var invalidToken = result.Errors.Any(e => e.Code is "InvalidToken");
            return BadRequest(new
            {
                message = invalidToken ? "Invalid or expired reset link." : "Could not reset your password.",
                errors = invalidToken ? null : result.Errors.Select(e => e.Description)
            });
        }

        return NoContent();
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

    // Self-service role upgrade (e.g. a Buyer deciding to list a property) — add-only, on
    // purpose: there's no path here to remove a role, which would otherwise force a decision
    // about what happens to an Agent's existing directory profile or an Owner's existing
    // listings. Returns a fresh token (same shape as Login/Google) because the JWT a visitor is
    // already holding has the OLD roles baked in — [Authorize(Roles=...)] checks and the
    // frontend's roleGuard both read from that token/its cached user, not a live DB lookup, so
    // without a new one here the new role would silently not take effect until the next login.
    [Authorize]
    [HttpPost("role")]
    public async Task<ActionResult<AuthResponseDto>> AddRole(AddRoleDto dto)
    {
        if (dto.Role is not ("Owner" or "Agent"))
            return BadRequest(new { message = "Role must be \"Owner\" or \"Agent\"." });

        var email = User.TryGetEmail() ?? User.Identity?.Name;
        var user = await _userManager.FindByEmailAsync(email!);
        if (user is null) return Unauthorized();

        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToAdd = RolesFor(dto.Role).Except(currentRoles).ToArray();

        if (rolesToAdd.Length > 0)
        {
            foreach (var role in rolesToAdd)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }

            await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        return await BuildAuthResponse(user);
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
