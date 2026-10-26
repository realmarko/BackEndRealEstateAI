using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/page-views")]
public class PageViewsController : ControllerBase
{
    // Well above any real Angular route pattern/path in this app, far enough under Kestrel's
    // body cap that an anonymous, rate-limited-by-count-only endpoint can't be used to write
    // oversized rows into page_views — same reasoning as ErrorsController's own length caps.
    private const int MaxFieldLength = 200;
    private const int DefaultWeeks = 8;
    private const int MaxWeeks = 26;

    private readonly ApplicationDbContext _db;

    public PageViewsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // POST /api/page-views — fired from the frontend's App root component on every Angular
    // NavigationEnd (see app.ts), sitewide, for every visitor. Anonymous and uncounted toward
    // any per-entity total on purpose: this is raw site traffic, not the existing per-listing/
    // per-agent "unique view" counters (ListingsController.RecordView et al.), which stay
    // deduplicated and untouched by this endpoint.
    [HttpPost]
    [EnableRateLimiting("page-view")]
    public async Task<IActionResult> Record(PageViewRecordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PathPattern) && string.IsNullOrWhiteSpace(dto.Path))
            return BadRequest(new { message = "pathPattern or path is required." });
        if (dto.PathPattern.Length > MaxFieldLength || dto.Path.Length > MaxFieldLength)
            return BadRequest(new { message = "pathPattern or path is too long." });

        _db.PageViews.Add(new PageView { PathPattern = dto.PathPattern, Path = dto.Path });
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/page-views/stats?weeks=8 — admin-only, never surfaced anywhere public. Groups by
    // the ISO week each view fell in and by PathPattern (not by the individual entity each
    // dynamic route pointed at — see PageView's own doc comment for why), most recent week and
    // most-viewed page first.
    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<WeeklyPageViewsDto>>> Stats([FromQuery] int weeks = DefaultWeeks)
    {
        weeks = Math.Clamp(weeks, 1, MaxWeeks);
        var cutoff = DateTime.UtcNow.AddDays(-7 * weeks);

        // Grouped in memory rather than via a Postgres date_trunc translation — the admin-only,
        // last-N-weeks window this endpoint queries keeps the row count small enough that
        // fetching (ViewedAt, PathPattern) pairs and bucketing them here is simpler than fighting
        // EF/Npgsql's function-translation mapping for a feature this low-traffic.
        var rows = await _db.PageViews
            .AsNoTracking()
            .Where(v => v.ViewedAt >= cutoff)
            .Select(v => new { v.ViewedAt, v.PathPattern })
            .ToListAsync();

        var result = rows
            .GroupBy(v => StartOfIsoWeek(v.ViewedAt))
            .OrderByDescending(wg => wg.Key)
            .Select(wg => new WeeklyPageViewsDto
            {
                WeekStart = wg.Key,
                Pages = wg
                    .GroupBy(v => v.PathPattern)
                    .Select(pg => new PagePathViewsDto { PathPattern = pg.Key, Views = pg.Count() })
                    .OrderByDescending(p => p.Views)
                    .ToList()
            })
            .ToList();

        return Ok(result);
    }

    // Monday of the ISO-8601 week the given UTC date falls in, at midnight — the bucket key
    // WeeklyPageViewsDto.WeekStart reports.
    private static DateTime StartOfIsoWeek(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        // DayOfWeek is Sunday=0..Saturday=6; ISO weeks start Monday, so Sunday needs to roll back
        // 6 days instead of -1.
        var daysSinceMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.Date.AddDays(-daysSinceMonday);
    }
}
