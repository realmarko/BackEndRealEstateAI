using Microsoft.AspNetCore.Routing;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Middleware;

// Safety net for exceptions no controller already catches — GeomarketingController, for example,
// handles its own known failure modes (DENUE unreachable, a bad AGEB polygon) and never reaches
// this. What lands here is genuinely unexpected, so every one of these is worth both a Sentry
// event (full trace, breadcrumbs) and a row in ErrorLog (so it shows up in the in-app admin list
// without needing a Sentry account).
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IErrorLogService errorLogService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            _logger.LogError(ex, "Unhandled exception at {Path}", context.Request.Path);

            try
            {
                // The route PATTERN ("/api/listings/{id}"), not the resolved path — grouping by
                // section is the whole point of this field, and every distinct listing/agent id
                // would otherwise make every row's Section unique.
                var section = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value ?? "unknown";

                // CancellationToken.None, not context.RequestAborted: a request that fails BECAUSE
                // the client disconnected is exactly the kind of error this table exists to catch,
                // but RequestAborted is already cancelled by the time we get here for that case —
                // using it would make SaveChangesAsync throw immediately and silently drop the
                // write for that entire class of error (Sentry would still get it, ErrorLog wouldn't).
                await errorLogService.LogAsync(
                    ErrorSource.Backend,
                    ErrorSeverity.Critical,
                    section,
                    ex.Message,
                    ex.ToString(),
                    context.User.TryGetUserId(),
                    context.User.TryGetEmail(),
                    context.Request.Headers.UserAgent.ToString(),
                    CancellationToken.None);
            }
            catch (Exception logEx)
            {
                // The DB write itself failing (e.g. the outage IS the database) must never mask
                // the original error or crash the request pipeline a second time — Sentry already
                // has the real exception above regardless of whether this write lands.
                _logger.LogError(logEx, "Failed to write ErrorLog for the exception above");
            }

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Something went wrong. Please try again." });
            }
        }
    }
}
