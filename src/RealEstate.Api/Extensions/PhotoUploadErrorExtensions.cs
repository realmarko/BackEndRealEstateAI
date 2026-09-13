using Microsoft.AspNetCore.Mvc;
using RealEstate.Api.Services;

namespace RealEstate.Api.Extensions;

public static class PhotoUploadErrorExtensions
{
    /// <summary>Maps a PhotoUploadErrorKind to the HTTP response a controller should return
    /// for it — shared so AgentsController and ListingsController return identical error
    /// bodies for the same underlying failure.</summary>
    public static ActionResult ToActionResult(this PhotoUploadErrorKind kind, ControllerBase controller) =>
        kind switch
        {
            PhotoUploadErrorKind.InvalidType =>
                controller.BadRequest(new { message = "Photos must be JPEG, PNG, or WEBP images." }),
            PhotoUploadErrorKind.TooLarge =>
                controller.BadRequest(new { message = "Each photo must be 5 MB or smaller." }),
            PhotoUploadErrorKind.UploadFailed =>
                controller.StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Could not upload one or more photos right now. Please try again." }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError)
        };
}
