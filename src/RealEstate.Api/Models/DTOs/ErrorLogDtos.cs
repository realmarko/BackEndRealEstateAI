namespace RealEstate.Api.Models.DTOs;

// What the frontend's own error handler posts when it catches something itself — see
// ErrorsController and the Angular GlobalErrorHandler.
public class ReportErrorDto
{
    public string Section { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
}

public class ErrorLogDetailDto
{
    public Guid Id { get; set; }
    public string? StackTrace { get; set; }
}

// One row per distinct (Source, Severity, Section, Message) combination matching the current
// filters, not per raw ErrorLog record — the same exception thrown 40 times shows up once here
// with Count = 40, so an admin scanning the list sees "this is happening a lot" instead of the
// same message 40 times in a row. Per-occurrence detail (exact user, exact timestamp) is only
// available for the specific sample below, which is an inherent, accepted trade-off of grouping.
public class ErrorLogGroupDto
{
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Count { get; set; }
    // How many of those Count occurrences are still unresolved — under the "unresolved"/
    // "resolved" filters this always equals Count/0 respectively (the filter already guarantees
    // it), but under "all" a partially-resolved group's own Resolved is false while Count still
    // includes the already-resolved rows; the "resolve all" button reads this, not Count, so its
    // label states how many rows the action will actually touch.
    public int UnresolvedCount { get; set; }
    public DateTime FirstOccurredAt { get; set; }
    public DateTime LastOccurredAt { get; set; }
    // The most recently occurred row in the group — GetById/{id}'s stack trace and Resolve's
    // single-row endpoint both still work against it, so the frontend's existing "show stack
    // trace" affordance needs no separate group-detail endpoint.
    public Guid SampleId { get; set; }
    // Whether that sample row specifically has a trace — same semantics as ErrorLogDto's own
    // HasStackTrace, just evaluated against the sample instead of a single fetched row.
    public bool HasStackTrace { get; set; }
    // Who most recently hit this error, and from what — the sample's own values. For a Count of 1
    // this is simply the one occurrence's user, same as before grouping existed; for a higher
    // count it's only the latest of possibly several different users, which is an accepted
    // trade-off of aggregating (see ErrorLogGroupDto's own remarks) rather than showing every
    // affected user inline.
    public string? SampleUserEmail { get; set; }
    public string? SampleUserAgent { get; set; }
    public bool Resolved { get; set; }
}

public class ResolveGroupDto
{
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
