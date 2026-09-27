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
        string frontendBaseUrl = "https://dev.espacial.com.mx")
    {
        return new AuthController(
            userManager.Object,
            MockRoleManager().Object,
            (tokenService ?? new Mock<ITokenService>()).Object,
            (emailService ?? new Mock<IEmailService>()).Object,
            NullLogger<AuthController>.Instance,
            Options.Create(new FrontendOptions { BaseUrl = frontendBaseUrl }));
    }

    private static ApplicationUser MakeUser(string email = "user@example.com") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        UserName = email,
        FirstName = "Marco",
        LastName = "Martinez"
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
}
