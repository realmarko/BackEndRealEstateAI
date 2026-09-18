using RealEstate.Api.Data;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IErrorLogService
{
    Task LogAsync(
        ErrorSource source,
        ErrorSeverity severity,
        string section,
        string message,
        string? stackTrace,
        Guid? userId,
        string? userEmail,
        string? userAgent,
        CancellationToken cancellationToken = default);
}

public class ErrorLogService : IErrorLogService
{
    // A full stack trace (especially minified JS, or .NET's with async state machines) can run to
    // tens of KB — the complete one is always in Sentry; this is just enough to triage from the
    // in-app admin list without ballooning row size.
    private const int MaxStackTraceLength = 4000;

    private readonly ApplicationDbContext _db;

    public ErrorLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        ErrorSource source,
        ErrorSeverity severity,
        string section,
        string message,
        string? stackTrace,
        Guid? userId,
        string? userEmail,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var entry = new ErrorLog
        {
            Source = source,
            Severity = severity,
            Section = section,
            Message = message,
            StackTrace = Truncate(stackTrace, MaxStackTraceLength),
            UserId = userId,
            UserEmail = userEmail,
            UserAgent = Truncate(userAgent, 500)
        };

        _db.ErrorLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
