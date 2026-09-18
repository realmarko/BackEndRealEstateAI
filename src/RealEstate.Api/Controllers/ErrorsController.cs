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

    // GET /api/errors?resolved=false&section=/api/listings&page=1&pageSize=20
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<ErrorLogDto>>> List(
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

        var totalCount = await query.CountAsync();
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        // StackTrace deliberately excluded here — up to 4000 chars each (see ErrorLogService),
        // and the admin UI only ever shows it for the one row a viewer expands, via the dedicated
        // GetById below. Pulling it for every row on every page load/filter/paginate would be
        // mostly-wasted DB and network I/O for text almost never read.
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(e => new ErrorLogDto
            {
                Id = e.Id,
                OccurredAt = e.OccurredAt,
                Source = e.Source.ToString(),
                Severity = e.Severity.ToString(),
                Section = e.Section,
                Message = e.Message,
                HasStackTrace = e.StackTrace != null,
                UserId = e.UserId,
                UserEmail = e.UserEmail,
                UserAgent = e.UserAgent,
                Resolved = e.Resolved,
                ResolvedAt = e.ResolvedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<ErrorLogDto> { Items = items, Page = safePage, PageSize = safePageSize, TotalCount = totalCount });
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
}
