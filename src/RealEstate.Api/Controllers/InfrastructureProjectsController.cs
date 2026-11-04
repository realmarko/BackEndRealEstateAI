using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

// Admin-captured announced infrastructure projects (highway expansions, new industrial parks,
// transit lines, hospitals, schools) — see InfrastructureProject's own doc comment for why this
// is manual data, not detected/scraped like Fraccionamiento. Feeds ViabilityAnalysisService and
// AppreciationPotentialService via InfrastructureProjectProximity, and the public /published
// endpoint powers the /map layer.
[ApiController]
[Route("api/infrastructure-projects")]
public class InfrastructureProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public InfrastructureProjectsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/infrastructure-projects/published — anonymous, powers the /map layer and is what
    // an admin viewer would expect to see live without logging in. No status query param accepted
    // (same reasoning as FraccionamientosController.ListPublished): hardcoding the Cancelled
    // exclusion here means this can never leak a retired project even by mistake.
    [HttpGet("published")]
    public async Task<ActionResult<List<InfrastructureProjectPublicDto>>> ListPublished()
    {
        var items = await _db.InfrastructureProjects.AsNoTracking()
            .Where(p => p.Status != InfrastructureProjectStatus.Cancelled)
            .OrderBy(p => p.AnnouncedDate)
            .Select(p => new InfrastructureProjectPublicDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type.ToString(),
                Status = p.Status.ToString(),
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                AffectedRadiusMeters = p.AffectedRadiusMeters,
                AnnouncedDate = p.AnnouncedDate,
                ExpectedCompletionDate = p.ExpectedCompletionDate,
                SourceUrl = p.SourceUrl
            })
            .ToListAsync();

        return Ok(items);
    }

    // GET /api/infrastructure-projects?page=1&pageSize=20 — the admin management list, every
    // status included (unlike /published).
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<InfrastructureProjectListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.InfrastructureProjects.AsNoTracking();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(p => new InfrastructureProjectListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Type = p.Type.ToString(),
                Status = p.Status.ToString(),
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                AnnouncedDate = p.AnnouncedDate,
                ExpectedCompletionDate = p.ExpectedCompletionDate
            })
            .ToListAsync();

        return Ok(new PagedResult<InfrastructureProjectListItemDto> { Items = items, Page = safePage, PageSize = safePageSize, TotalCount = totalCount });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InfrastructureProjectDetailDto>> GetById(Guid id)
    {
        var entity = await _db.InfrastructureProjects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (entity is null) return NotFound();

        return Ok(ToDetailDto(entity));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InfrastructureProjectDetailDto>> Create(InfrastructureProjectUpsertDto dto)
    {
        var entity = new InfrastructureProject { CreatedByUserId = User.GetUserId() };
        ApplyUpsert(entity, dto);
        // Every new project starts Announced regardless of what the form sent — Status only ever
        // moves forward from here via a later Update (Announced -> InProgress -> Completed) or
        // retires via the dedicated Cancel action below, never set arbitrarily at creation.
        entity.Status = InfrastructureProjectStatus.Announced;

        _db.InfrastructureProjects.Add(entity);
        await _db.SaveChangesAsync();

        return Ok(ToDetailDto(entity));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InfrastructureProjectDetailDto>> Update(Guid id, InfrastructureProjectUpsertDto dto)
    {
        var entity = await _db.InfrastructureProjects.FindAsync(id);
        if (entity is null) return NotFound();

        // Cancelled is a one-way soft-retire (see Cancel below) — this generic edit form must
        // never be able to un-cancel a project or cancel one behind that action's back, or the
        // two paths fight each other over what "cancelled" means.
        if (entity.Status == InfrastructureProjectStatus.Cancelled)
            return BadRequest(new { message = "This project is cancelled and can no longer be edited." });
        if (dto.Status == InfrastructureProjectStatus.Cancelled)
            return BadRequest(new { message = "Use the cancel action to retire a project, not this form." });

        ApplyUpsert(entity, dto);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ToDetailDto(entity));
    }

    private static void ApplyUpsert(InfrastructureProject entity, InfrastructureProjectUpsertDto dto)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Type = dto.Type;
        entity.Status = dto.Status;
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.AffectedRadiusMeters = dto.AffectedRadiusMeters;
        entity.AnnouncedDate = AsUtc(dto.AnnouncedDate);
        entity.ExpectedCompletionDate = AsUtc(dto.ExpectedCompletionDate);
        entity.SourceUrl = dto.SourceUrl;
    }

    // PATCH /{id}/cancel — soft-retire instead of a hard delete, same reasoning as
    // FraccionamientosController's reject/merge actions.
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<InfrastructureProjectDetailDto>> Cancel(Guid id)
    {
        var entity = await _db.InfrastructureProjects.FindAsync(id);
        if (entity is null) return NotFound();

        entity.Status = InfrastructureProjectStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToDetailDto(entity));
    }

    // A plain JSON date (no "Z"/offset, e.g. "2026-10-01") deserializes with DateTime.Kind
    // Unspecified — Npgsql refuses to write that to a "timestamp with time zone" column
    // ("only UTC is supported"). Every date on this entity is admin-entered as a calendar date
    // with no real timezone meaning, so treating it as UTC is correct, not a lossy guess.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static InfrastructureProjectDetailDto ToDetailDto(InfrastructureProject p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Type = p.Type.ToString(),
        Status = p.Status.ToString(),
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        AffectedRadiusMeters = p.AffectedRadiusMeters,
        AnnouncedDate = p.AnnouncedDate,
        ExpectedCompletionDate = p.ExpectedCompletionDate,
        SourceUrl = p.SourceUrl,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
