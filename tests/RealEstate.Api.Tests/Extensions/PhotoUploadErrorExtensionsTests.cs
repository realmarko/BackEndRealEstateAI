using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Api.Extensions;
using RealEstate.Api.Services;
using Xunit;

namespace RealEstate.Api.Tests.Extensions;

public class PhotoUploadErrorExtensionsTests
{
    private class TestController : ControllerBase
    {
    }

    private static TestController BuildController() => new()
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        }
    };

    [Fact]
    public void ToActionResult_InvalidType_ReturnsBadRequest()
    {
        var result = PhotoUploadErrorKind.InvalidType.ToActionResult(BuildController());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void ToActionResult_TooLarge_ReturnsBadRequest()
    {
        var result = PhotoUploadErrorKind.TooLarge.ToActionResult(BuildController());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public void ToActionResult_UploadFailed_ReturnsBadGateway()
    {
        var result = PhotoUploadErrorKind.UploadFailed.ToActionResult(BuildController());

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, statusResult.StatusCode);
    }

    [Fact]
    public void ToActionResult_UnknownKind_ReturnsInternalServerError()
    {
        var result = ((PhotoUploadErrorKind)999).ToActionResult(BuildController());

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }
}
