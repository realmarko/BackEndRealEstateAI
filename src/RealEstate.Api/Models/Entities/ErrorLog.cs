namespace RealEstate.Api.Models.Entities;

// One row per error caught either by GlobalExceptionMiddleware (backend) or reported by the
// frontend's own error handler via POST /api/errors — the in-app counterpart to Sentry, which
// gets the same errors with full stack traces and breadcrumbs. This table exists so an admin can
// see "what broke, for whom, where" without leaving the app or needing a Sentry account.
public class ErrorLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public ErrorSource Source { get; set; }
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Error;

    // The backend request path (e.g. "/api/listings/123") or the frontend route
    // (e.g. "/listings/123") where the error happened — "section of the site" in plain terms.
    public string Section { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
    // Truncated to a few KB at write time (see ErrorLogService) — a full .NET stack trace or a
    // minified JS one can otherwise run to tens of KB per row for no real benefit; the full trace
    // is always still in Sentry.
    public string? StackTrace { get; set; }

    // Whoever was signed in when it happened, if anyone — null for an anonymous visitor.
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAgent { get; set; }

    public bool Resolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
}
