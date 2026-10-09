using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstate.Api.Controllers;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Tests.Controllers;

public class AuthControllerTests
{
    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole<Guid>>> MockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole<Guid>>>();
        return new Mock<RoleManager<IdentityRole<Guid>>>(store.Object, null!, null!, null!, null!);
    }

    private static AuthController MakeController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IEmailService>? emailService = null,
        Mock<ITokenService>? tokenService = null,
        Mock<IGoogleTokenValidator>? googleTokenValidator = null,
        Mock<IFacebookTokenValidator>? facebookTokenValidator = null,
        string frontendBaseUrl = "https://dev.espacial.com.mx",
        string googleClientId = "test-google-client-id")
    {
        return new AuthController(
            userManager.Object,
            MockRoleManager().Object,
            (tokenService ?? new Mock<ITokenService>()).Object,
            (emailService ?? new Mock<IEmailService>()).Object,
            NullLogger<AuthController>.Instance,
            (googleTokenValidator ?? new Mock<IGoogleTokenValidator>()).Object,
            (facebookTokenValidator ?? new Mock<IFacebookTokenValidator>()).Object,
            Options.Create(new FrontendOptions { BaseUrl = frontendBaseUrl }),
            Options.Create(new GoogleOptions { ClientId = googleClientId }),
            Options.Create(new FacebookOptions { AppId = "test-fb-app-id", AppSecret = "test-fb-app-secret" }));
    }

    // AddRole reads the caller's identity from ControllerBase.User (via TryGetEmail), unlike
    // every other endpoint tested here which takes the email as a DTO field — this stands in for
    // the ClaimsPrincipal an authenticated request would actually carry.
    private static void AuthenticateAs(AuthController controller, string email)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    // EmailConfirmed defaults true here: every existing test predates the email-verification
    // feature and is exercising already-verified-account behavior, not the new gate itself (see
    // Login_UnverifiedEmail_ReturnsForbidden for that).
    private static ApplicationUser MakeUser(string email = "user@example.com") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email,
        FirstName = "Marco",
        LastName = "Martinez",
        EmailConfirmed = true
    };

    // --- ForgotPassword: must never reveal whether the email is registered ---

    [Fact]
    public async Task ForgotPassword_UnregisteredEmail_ReturnsNoContentAndSendsNoEmail()
    {
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync("nobody@example.com")).ReturnsAsync((ApplicationUser?)null);
        var emailService = new Mock<IEmailService>();
        var controller = MakeController(userManager, emailService);

        var result = await controller.ForgotPassword(new ForgotPasswordDto { Email = "nobody@example.com" });

        Assert.IsType<NoContentResult>(result);
        emailService.Verify(
            e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_RegisteredEmail_ReturnsNoContentAndSendsResetEmail()
    {
        var user = MakeUser("user@example.com");
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("the-token");
        var emailService = new Mock<IEmailService>();
        var controller = MakeController(userManager, emailService, frontendBaseUrl: "https://dev.espacial.com.mx/");

        var result = await controller.ForgotPassword(new ForgotPasswordDto { Email = user.Email! });

        Assert.IsType<NoContentResult>(result);
        emailService.Verify(e => e.SendAsync(
            user.Email!,
            "Marco Martinez",
            It.IsAny<string>(),
            It.Is<string>(body =>
                body.Contains("https://dev.espacial.com.mx/reset-password") &&
                body.Contains("email=user%40example.com") &&
                body.Contains("token=the-token"))),
            Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WhenEmailSendThrows_StillReturnsNoContent()
    {
        // The whole point of this endpoint is a response that never differs between a registered
        // and an unregistered email — an SMTP failure must not turn into a 500 only registered
        // addresses can trigger.
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("tok");
        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));
        var controller = MakeController(userManager, emailService);

        var result = await controller.ForgotPassword(new ForgotPasswordDto { Email = user.Email! });

        Assert.IsType<NoContentResult>(result);
    }

    // --- ResetPassword ---

    [Fact]
    public async Task ResetPassword_UnknownEmail_ReturnsBadRequest()
    {
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync("nobody@example.com")).ReturnsAsync((ApplicationUser?)null);
        var controller = MakeController(userManager);

        var result = await controller.ResetPassword(new ResetPasswordDto
        {
            Email = "nobody@example.com",
            Token = "tok",
            NewPassword = "NewPassw0rd!"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid or expired reset link.", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequestWithGenericMessage()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager
            .Setup(m => m.ResetPasswordAsync(user, "bad-token", "NewPassw0rd!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "InvalidToken", Description = "Invalid token." }));
        var controller = MakeController(userManager);

        var result = await controller.ResetPassword(new ResetPasswordDto
        {
            Email = user.Email!,
            Token = "bad-token",
            NewPassword = "NewPassw0rd!"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        // Identity's own error code/description must never leak to the caller for this case —
        // only the generic "get a new link" message.
        Assert.DoesNotContain("Invalid token.", badRequest.Value!.ToString());
        Assert.Contains("Invalid or expired reset link.", badRequest.Value!.ToString());
    }

    [Fact]
    public async Task ResetPassword_OtherIdentityFailure_ReturnsBadRequestWithErrorDescriptions()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager
            .Setup(m => m.ResetPasswordAsync(user, "tok", "short"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Passwords must be at least 8 characters."
            }));
        var controller = MakeController(userManager);

        var result = await controller.ResetPassword(new ResetPasswordDto
        {
            Email = user.Email!,
            Token = "tok",
            NewPassword = "short"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var errors = (IEnumerable<string>)badRequest.Value!.GetType().GetProperty("errors")!.GetValue(badRequest.Value)!;
        Assert.Contains("Passwords must be at least 8 characters.", errors);
    }

    [Fact]
    public async Task ResetPassword_ValidTokenAndPassword_ReturnsNoContent()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager
            .Setup(m => m.ResetPasswordAsync(user, "tok", "NewPassw0rd!"))
            .ReturnsAsync(IdentityResult.Success);
        var controller = MakeController(userManager);

        var result = await controller.ResetPassword(new ResetPasswordDto
        {
            Email = user.Email!,
            Token = "tok",
            NewPassword = "NewPassw0rd!"
        });

        Assert.IsType<NoContentResult>(result);
    }

    // --- Login ---

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "wrong")).ReturnsAsync(false);
        var controller = MakeController(userManager);

        var result = await controller.Login(new LoginDto { Email = user.Email!, Password = "wrong" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync("nobody@example.com")).ReturnsAsync((ApplicationUser?)null);
        var controller = MakeController(userManager);

        var result = await controller.Login(new LoginDto { Email = "nobody@example.com", Password = "whatever" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokenAndUser()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct")).ReturnsAsync(true);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Buyer"]);
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.Is<IList<string>>(r => r.Contains("Buyer"))))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService);

        var result = await controller.Login(new LoginDto { Email = user.Email!, Password = "correct" });

        var ok = Assert.IsType<AuthResponseDto>(result.Value);
        Assert.Equal("jwt-token", ok.Token);
        Assert.Equal(user.Email, ok.User.Email);
    }

    [Fact]
    public async Task Login_UnverifiedEmail_ReturnsForbidden()
    {
        var user = MakeUser();
        user.EmailConfirmed = false;
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "correct")).ReturnsAsync(true);
        var controller = MakeController(userManager);

        var result = await controller.Login(new LoginDto { Email = user.Email!, Password = "correct" });

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    // --- Google sign-in ---

    [Fact]
    public async Task Google_InvalidIdToken_ReturnsUnauthorized()
    {
        var userManager = MockUserManager();
        var validator = new Mock<IGoogleTokenValidator>();
        validator
            .Setup(v => v.ValidateAsync("bad-token", "test-google-client-id"))
            .ThrowsAsync(new InvalidJwtException("bad signature"));
        var controller = MakeController(userManager, googleTokenValidator: validator);

        var result = await controller.Google(new GoogleAuthDto { IdToken = "bad-token" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Google_ExistingVerifiedUser_LogsIntoSameAccountWithoutChangingRoles()
    {
        var user = MakeUser("agent@example.com");
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Owner", "Agent"]);
        var validator = new Mock<IGoogleTokenValidator>();
        validator
            .Setup(v => v.ValidateAsync("good-token", "test-google-client-id"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload { Email = user.Email, GivenName = "New", FamilyName = "Name" });
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.Is<IList<string>>(r => r.Contains("Owner") && r.Contains("Agent"))))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService, googleTokenValidator: validator);

        var result = await controller.Google(new GoogleAuthDto { IdToken = "good-token", Role = "Buyer" });

        var ok = Assert.IsType<AuthResponseDto>(result.Value);
        Assert.Equal("jwt-token", ok.Token);
        Assert.Equal(user.Email, ok.User.Email);
        // Role from the Google payload must never override an existing account's roles — the
        // Google-first-time-only "Role" in the request (here "Buyer") is ignored entirely.
        userManager.Verify(m => m.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task Google_ExistingUnverifiedUser_ConfirmsEmailAndLogsIn()
    {
        var user = MakeUser();
        user.EmailConfirmed = false;
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Buyer"]);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var validator = new Mock<IGoogleTokenValidator>();
        validator
            .Setup(v => v.ValidateAsync("good-token", "test-google-client-id"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload { Email = user.Email });
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.IsAny<IList<string>>()))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService, googleTokenValidator: validator);

        var result = await controller.Google(new GoogleAuthDto { IdToken = "good-token" });

        Assert.IsType<AuthResponseDto>(result.Value);
        Assert.True(user.EmailConfirmed);
        userManager.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task Google_NewEmail_CreatesPreConfirmedAccountWithRequestedRole()
    {
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync("new.agent@example.com")).ReturnsAsync((ApplicationUser?)null);
        userManager
            .Setup(m => m.CreateAsync(
                It.Is<ApplicationUser>(u => u.Email == "new.agent@example.com" && u.EmailConfirmed && u.FirstName == "Ana" && u.LastName == "Lopez"),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.AddToRolesAsync(
                It.Is<ApplicationUser>(u => u.Email == "new.agent@example.com"),
                It.Is<IEnumerable<string>>(r => r.Contains("Owner") && r.Contains("Agent"))))
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.Email == "new.agent@example.com")))
            .ReturnsAsync(["Owner", "Agent"]);
        var validator = new Mock<IGoogleTokenValidator>();
        validator
            .Setup(v => v.ValidateAsync("good-token", "test-google-client-id"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload { Email = "new.agent@example.com", GivenName = "Ana", FamilyName = "Lopez" });
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService, googleTokenValidator: validator);

        var result = await controller.Google(new GoogleAuthDto { IdToken = "good-token", Role = "Agent" });

        var ok = Assert.IsType<AuthResponseDto>(result.Value);
        Assert.Equal("new.agent@example.com", ok.User.Email);
        userManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Once);
    }

    // --- AddRole (self-service role upgrade) ---

    [Fact]
    public async Task AddRole_InvalidRole_ReturnsBadRequestWithoutTouchingRoles()
    {
        var userManager = MockUserManager();
        var controller = MakeController(userManager);
        AuthenticateAs(controller, "user@example.com");

        var result = await controller.AddRole(new AddRoleDto { Role = "Buyer" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        userManager.Verify(m => m.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task AddRole_BuyerAddsAgent_GrantsBothOwnerAndAgent()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Buyer"]);
        userManager
            .Setup(m => m.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Owner", "Agent" }))))
            .ReturnsAsync(IdentityResult.Success);
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.IsAny<IList<string>>()))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService);
        AuthenticateAs(controller, user.Email!);

        var result = await controller.AddRole(new AddRoleDto { Role = "Agent" });

        var ok = Assert.IsType<AuthResponseDto>(result.Value);
        Assert.Equal("jwt-token", ok.Token);
        userManager.Verify(m => m.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Owner", "Agent" }))), Times.Once);
    }

    [Fact]
    public async Task AddRole_OwnerAddsAgent_OnlyGrantsTheMissingAgentRole()
    {
        // Already has Owner — RolesFor("Agent") is ["Owner","Agent"], so only "Agent" is
        // actually new. Identity's AddToRolesAsync fails a role the user is already in, so
        // re-sending "Owner" here would be a bug, not just redundant.
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Owner"]);
        userManager
            .Setup(m => m.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Agent" }))))
            .ReturnsAsync(IdentityResult.Success);
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.IsAny<IList<string>>()))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService);
        AuthenticateAs(controller, user.Email!);

        var result = await controller.AddRole(new AddRoleDto { Role = "Agent" });

        Assert.IsType<AuthResponseDto>(result.Value);
        userManager.Verify(m => m.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Agent" }))), Times.Once);
    }

    [Fact]
    public async Task AddRole_AlreadyHasRequestedRole_IsANoOpThatStillReturnsAFreshToken()
    {
        var user = MakeUser();
        var userManager = MockUserManager();
        userManager.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Owner", "Agent"]);
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.CreateToken(user, It.IsAny<IList<string>>()))
            .Returns(("jwt-token", DateTime.UtcNow.AddDays(1)));
        var controller = MakeController(userManager, tokenService: tokenService);
        AuthenticateAs(controller, user.Email!);

        var result = await controller.AddRole(new AddRoleDto { Role = "Owner" });

        var ok = Assert.IsType<AuthResponseDto>(result.Value);
        Assert.Equal("jwt-token", ok.Token);
        userManager.Verify(m => m.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }
}
