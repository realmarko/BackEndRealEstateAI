using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/errors")]
public class ErrorsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IErrorLogService _errorLogService;

    public ErrorsController(ApplicationDbContext db, IErrorLogService errorLogService)
    {
        _db = db;
        _errorLogService = errorLogService;
    }

    // Well above anything a legitimate route/error message needs, but far enough under Kestrel's
    // request-body cap that a scripted client can't use this anonymous, rate-limited-by-count-only
    // endpoint to write multi-MB rows into error_logs (and burn Sentry's event quota) just by
    // sending a handful of oversized requests per minute.
    private const int MaxSectionLength = 500;
    private const int MaxMessageLength = 2000;

    // POST /api/errors — the frontend's GlobalErrorHandler posts here for anything it catches
    // itself (a component throwing, a broken template). Anonymous on purpose: an error can just
    // as easily happen to a visitor who isn't signed in, and this shouldn't need a token to work
    // when the app is already in a broken state. Rate-limited instead, since it's an anonymous
    // write endpoint — a script hammering it would otherwise flood ErrorLog and Sentry alike.
    [HttpPost]
    [EnableRateLimiting("errors")]
    public async Task<IActionResult> Report(ReportErrorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Section) || string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { message = "section and message are required." });
        if (dto.Section.Length > MaxSectionLength || dto.Message.Length > MaxMessageLength)
            return BadRequest(new { message = "section or message is too long." });

        SentrySdk.CaptureMessage($"[frontend] {dto.Message}", scope =>
        {
            scope.SetTag("section", dto.Section);
            if (dto.Stack is not null) scope.SetExtra("stack", dto.Stack);
        });

        try
        {
            await _errorLogService.LogAsync(
                ErrorSource.Frontend,
                ErrorSeverity.Error,
                dto.Section,
                dto.Message,
                dto.Stack,
                User.TryGetUserId(),
                User.TryGetEmail(),
                Request.Headers.UserAgent.ToString());
        }
        catch (Exception ex)
        {
            // Same reasoning as GlobalExceptionMiddleware's own guard around this call: if the DB
            // write fails (e.g. during an outage), Sentry already has the report above — letting
            // this throw would otherwise propagate to that same middleware and log a second,
            // differently-labeled "Unhandled exception at /api/errors" entry for what was really
            // just the DB being down, doubling the noise during the exact window it matters least.
            SentrySdk.CaptureException(ex);
        }

        return NoContent();
    }

    // GET /api/errors?resolved=false&section=/api/listings&page=1&pageSize=10 — rows are grouped
    // by (Source, Severity, Section, Message) so the same exception firing repeatedly shows up
    // once with a Count, not as a wall of identical cards; see ErrorLogGroupDto.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<ErrorLogGroupDto>>> List(
        [FromQuery] bool? resolved,
        [FromQuery] ErrorSource? source,
        [FromQuery] string? section,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.ErrorLogs.AsNoTracking().AsQueryable();
        if (resolved.HasValue) query = query.Where(e => e.Resolved == resolved.Value);
        if (source.HasValue) query = query.Where(e => e.Source == source.Value);
        if (!string.IsNullOrWhiteSpace(section)) query = query.Where(e => EF.Functions.ILike(e.Section, $"%{section}%"));

        // Count of distinct signatures only — deliberately not counted from `grouped` below,
        // which also computes the four sample subqueries per group; those are wasted work for a
        // path that only needs how many groups exist, not their content.
        var totalCount = await query
            .GroupBy(e => new { e.Source, e.Severity, e.Section, e.Message })
            .CountAsync();

        var grouped = query
            .GroupBy(e => new { e.Source, e.Severity, e.Section, e.Message })
            .Select(g => new
            {
                g.Key.Source,
                g.Key.Severity,
                g.Key.Section,
                g.Key.Message,
                Count = g.Count(),
                UnresolvedCount = g.Count(e => !e.Resolved),
                FirstOccurredAt = g.Min(e => e.OccurredAt),
                LastOccurredAt = g.Max(e => e.OccurredAt),
                // Only true once every occurrence sharing this signature is resolved — under the
                // resolved=true/false filters every row in a group already agrees (the filter
                // itself guarantees it); this only does real work under the "all" filter, where a
                // signature that recurred after being resolved should still read as unresolved.
                Resolved = !g.Any(e => !e.Resolved),
                // The single most recent occurrence in the group — its id is reused for the
                // existing GetById/{id} stack-trace lookup (so expanding a group needs no new
                // endpoint), and its user/trace fields stand in for the group in the summary row.
                // Four separate correlated subqueries rather than one returning an anonymous
                // object: EF Core/Npgsql can translate "ORDER BY ... LIMIT 1" per scalar column
                // this way, but couldn't translate a single subquery projecting multiple columns
                // at once here (threw "could not be translated" at request time — caught by
                // actually calling this endpoint, not just by it compiling). Each subquery also
                // breaks ties on Id — OccurredAt alone can tie within the same clock tick during
                // a burst of identical errors, and an untied order isn't guaranteed to resolve
                // all four subqueries to the same physical row.
                SampleId = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.Id).First(),
                SampleHasStackTrace = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.StackTrace != null).First(),
                SampleUserEmail = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.UserEmail).First(),
                SampleUserAgent = g.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).Select(e => e.UserAgent).First()
            });

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var items = await grouped
            .OrderByDescending(g => g.LastOccurredAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(g => new ErrorLogGroupDto
            {
                Source = g.Source.ToString(),
                Severity = g.Severity.ToString(),
                Section = g.Section,
                Message = g.Message,
                Count = g.Count,
                UnresolvedCount = g.UnresolvedCount,
                FirstOccurredAt = g.FirstOccurredAt,
                LastOccurredAt = g.LastOccurredAt,
                SampleId = g.SampleId,
                HasStackTrace = g.SampleHasStackTrace,
                SampleUserEmail = g.SampleUserEmail,
                SampleUserAgent = g.SampleUserAgent,
                Resolved = g.Resolved
            })
            .ToListAsync();

        return Ok(new PagedResult<ErrorLogGroupDto> { Items = items, Page = safePage, PageSize = safePageSize, TotalCount = totalCount });
    }

    // GET /api/errors/{id} — full detail including StackTrace, fetched only when the admin
    // expands one row (see List's own comment for why the list itself omits it).
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ErrorLogDetailDto>> GetById(Guid id)
    {
        var entry = await _db.ErrorLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null) return NotFound();

        return Ok(new ErrorLogDetailDto { Id = entry.Id, StackTrace = entry.StackTrace });
    }

    // PATCH /api/errors/{id}/resolve
    [HttpPatch("{id:guid}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resolve(Guid id)
    {
        var entry = await _db.ErrorLogs.FindAsync(id);
        if (entry is null) return NotFound();

        entry.Resolved = true;
        entry.ResolvedAt = DateTime.UtcNow;
        entry.ResolvedByUserId = User.GetUserId();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PATCH /api/errors/resolve-group — resolves every currently-unresolved occurrence sharing
    // a signature at once, so clicking "Resolve" on a grouped row (Count = 40) doesn't leave 39
    // identical rows still sitting in the unresolved list.
    [HttpPatch("resolve-group")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResolveGroup(ResolveGroupDto dto)
    {
        if (!Enum.TryParse<ErrorSource>(dto.Source, out var source) || !Enum.TryParse<ErrorSeverity>(dto.Severity, out var severity))
            return BadRequest(new { message = "Invalid source or severity." });

        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        // No NotFound-on-zero-updated: 0 rows can mean "already resolved" just as easily as
        // "never existed" (a second click, a second admin tab, or this same signature resolving
        // via the "all" filter's own rollup) — resolving an already-resolved group is a no-op,
        // not a failure, and treating it as one surfaced a false error toast for a request that
        // actually left the group in the exact state the admin wanted.
        await _db.ErrorLogs
            .Where(e => e.Source == source && e.Severity == severity && e.Section == dto.Section && e.Message == dto.Message && !e.Resolved)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Resolved, true)
                .SetProperty(e => e.ResolvedAt, now)
                .SetProperty(e => e.ResolvedByUserId, userId));

        return NoContent();
    }
}
