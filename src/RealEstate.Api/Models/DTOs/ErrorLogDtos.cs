namespace RealEstate.Api.Models.DTOs;

// What the frontend's own error handler posts when it catches something itself — see
// ErrorsController and the Angular GlobalErrorHandler.
public class ReportErrorDto
{
    public string Section { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
}

public class ErrorLogDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }

    // Exposed as their enum member name (e.g. "Backend", "Critical") rather than the raw int
    // System.Text.Json would otherwise emit — same convention as Inquiry.FundingMethod. No
    // reverse mapping needed like that one has, since these are read-only display fields here.
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;

    public string Section { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    // Not the trace itself — see ErrorsController.List's comment. Truthy means the detail
    // endpoint (GET /api/errors/{id}) has something to show; the admin UI only calls it then.
    public bool HasStackTrace { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserAgent { get; set; }
    public bool Resolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ErrorLogDetailDto
{
    public Guid Id { get; set; }
    public string? StackTrace { get; set; }
}
