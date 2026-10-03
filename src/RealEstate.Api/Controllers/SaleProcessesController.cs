using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

// The agent's sale pipeline: one SaleProcess per client walking through the sell-a-house
// workflow (prospección → post-venta), with a document checklist and a task list per deal.
// Every endpoint is scoped to the calling agent's own SaleProcesses — an agent never sees or
// edits another agent's pipeline.
[ApiController]
[Route("api")]
[Authorize(Roles = "Agent")]
public class SaleProcessesController : ControllerBase
{
    private const int FinalStageId = 9; // Post-venta — excluded from "active" and counted as "closed"

    private readonly ApplicationDbContext _db;
    private readonly ISaleProcessService _saleProcessService;

    public SaleProcessesController(ApplicationDbContext db, ISaleProcessService saleProcessService)
    {
        _db = db;
        _saleProcessService = saleProcessService;
    }

    [HttpGet("pipeline-stages")]
    public async Task<ActionResult<List<PipelineStageDto>>> GetStages()
    {
        var stages = await _db.PipelineStages
            .OrderBy(s => s.SortOrder)
            .Select(s => new PipelineStageDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                SortOrder = s.SortOrder,
                RequiresLegalReview = s.RequiresLegalReview
            })
            .ToListAsync();

        return Ok(stages);
    }

    [HttpGet("sale-processes")]
    public async Task<ActionResult<List<SaleProcessDto>>> GetAll()
    {
        var agentId = User.GetUserId();
        var processes = await _db.SaleProcesses
            .Include(p => p.CurrentStage)
            .Include(p => p.Documents)
            .Include(p => p.Tasks)
            .Where(p => p.AgentUserId == agentId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        return Ok(processes.Select(ToSummaryDto));
    }

    [HttpGet("sale-processes/metrics")]
    public async Task<ActionResult<PipelineMetricsDto>> GetMetrics()
    {
        var agentId = User.GetUserId();
        var processes = await _db.SaleProcesses
            .Include(p => p.CurrentStage)
            .Where(p => p.AgentUserId == agentId)
            .ToListAsync();

        return Ok(new PipelineMetricsDto
        {
            ActiveCount = processes.Count(p => p.CurrentStageId != FinalStageId),
            // Only active deals, matching ActiveCount — a closed (post-venta) deal's price has
            // already left the pipeline, so counting it here would inflate "pipeline value" with
            // revenue that isn't pending anymore.
            TotalValue = processes.Where(p => p.CurrentStageId != FinalStageId).Sum(p => p.EstimatedPrice),
            LegalReviewCount = processes.Count(p => p.CurrentStage?.RequiresLegalReview == true),
            ClosedCount = processes.Count(p => p.CurrentStageId == FinalStageId)
        });
    }

    [HttpGet("sale-processes/{id:guid}")]
    public async Task<ActionResult<SaleProcessDetailDto>> GetById(Guid id)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        return Ok(ToDetailDto(process));
    }

    [HttpPost("sale-processes")]
    public async Task<ActionResult<SaleProcessDetailDto>> Create(SaleProcessCreateDto dto)
    {
        if (dto.EstimatedPrice <= 0)
            return BadRequest(new { message = "Estimated price must be greater than zero." });

        var agentId = User.GetUserId();
        var stageId = dto.StageId ?? 1;
        var stage = await _db.PipelineStages.FindAsync(stageId);
        if (stage is null)
            return BadRequest(new { message = "Unknown stage id." });

        // Only a listing the caller owns can be linked — otherwise a crafted ListingId would
        // either FK-fail at SaveChanges (surfacing as a 500) or silently attach this sale
        // process to a property that belongs to someone else.
        if (dto.ListingId is not null && !await _db.Listings.AnyAsync(l => l.Id == dto.ListingId && l.OwnerId == agentId))
            return BadRequest(new { message = "Listing not found." });

        var process = new SaleProcess
        {
            AgentUserId = agentId,
            ClientName = dto.ClientName,
            ClientPhone = dto.ClientPhone,
            PropertyAddress = dto.PropertyAddress,
            EstimatedPrice = dto.EstimatedPrice,
            ListingId = dto.ListingId,
            CurrentStageId = stageId,
            CurrentStage = stage,
            Tasks = { new SaleProcessTask { Title = "Primer contacto y confirmación de datos" } }
        };

        await _saleProcessService.SyncStageDocumentsAsync(process, stageId);

        _db.SaleProcesses.Add(process);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = process.Id }, ToDetailDto(process));
    }

    [HttpPatch("sale-processes/{id:guid}/stage")]
    public async Task<ActionResult<SaleProcessDetailDto>> UpdateStage(Guid id, SaleProcessStageUpdateDto dto)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();
        var stage = await _db.PipelineStages.FindAsync(dto.StageId);
        if (stage is null)
            return BadRequest(new { message = "Unknown stage id." });

        if (process.CurrentStageId != dto.StageId)
        {
            process.CurrentStageId = dto.StageId;
            process.CurrentStage = stage;
            process.UpdatedAt = DateTime.UtcNow;
            await _saleProcessService.SyncStageDocumentsAsync(process, dto.StageId);
            await _db.SaveChangesAsync();
        }

        return Ok(ToDetailDto(process));
    }

    [HttpDelete("sale-processes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        _db.SaleProcesses.Remove(process);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("sale-processes/{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid id, Guid documentId, SaleProcessDocumentUpdateDto dto)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        var document = process.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return NotFound();

        document.IsVerified = dto.IsVerified;
        document.VerifiedAt = dto.IsVerified ? DateTime.UtcNow : null;
        process.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("sale-processes/{id:guid}/tasks")]
    public async Task<ActionResult<SaleProcessTaskDto>> AddTask(Guid id, SaleProcessTaskCreateDto dto)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        var task = new SaleProcessTask { SaleProcessId = process.Id, Title = dto.Title };
        // Added via the DbSet, not process.Tasks.Add(...): same reasoning as
        // SaleProcessService.SyncStageDocumentsAsync — the Id property initializer already set
        // this entity's key, so adding it only through an already-tracked parent's navigation
        // gets read as "this row already exists" and produces a failing UPDATE instead of an
        // INSERT.
        _db.SaleProcessTasks.Add(task);
        process.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new SaleProcessTaskDto { Id = task.Id, Title = task.Title, IsCompleted = task.IsCompleted, CompletedAt = task.CompletedAt });
    }

    [HttpPatch("sale-processes/{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, Guid taskId, SaleProcessTaskUpdateDto dto)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        var task = process.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return NotFound();

        task.IsCompleted = dto.IsCompleted;
        task.CompletedAt = dto.IsCompleted ? DateTime.UtcNow : null;
        process.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("sale-processes/{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, Guid taskId)
    {
        var process = await FindOwnedAsync(id);
        if (process is null) return NotFound();

        var task = process.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return NotFound();

        process.Tasks.Remove(task);
        process.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Scoped to the current agent on every call — never just FirstOrDefaultAsync(id) — so one
    // agent can't read or mutate another agent's pipeline by guessing/reusing a SaleProcess id.
    private async Task<SaleProcess?> FindOwnedAsync(Guid id)
    {
        var agentId = User.GetUserId();
        return await _db.SaleProcesses
            .Include(p => p.CurrentStage)
            .Include(p => p.Documents)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id && p.AgentUserId == agentId);
    }

    private static SaleProcessDto ToSummaryDto(SaleProcess p) => new()
    {
        Id = p.Id,
        ClientName = p.ClientName,
        ClientPhone = p.ClientPhone,
        PropertyAddress = p.PropertyAddress,
        EstimatedPrice = p.EstimatedPrice,
        ListingId = p.ListingId,
        CurrentStageId = p.CurrentStageId,
        CurrentStageName = p.CurrentStage?.Name ?? string.Empty,
        RequiresLegalReview = p.CurrentStage?.RequiresLegalReview ?? false,
        DocumentsTotal = p.Documents.Count,
        DocumentsVerified = p.Documents.Count(d => d.IsVerified),
        TasksTotal = p.Tasks.Count,
        TasksCompleted = p.Tasks.Count(t => t.IsCompleted),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private static SaleProcessDetailDto ToDetailDto(SaleProcess p)
    {
        var summary = ToSummaryDto(p);
        return new SaleProcessDetailDto
        {
            Id = summary.Id,
            ClientName = summary.ClientName,
            ClientPhone = summary.ClientPhone,
            PropertyAddress = summary.PropertyAddress,
            EstimatedPrice = summary.EstimatedPrice,
            ListingId = summary.ListingId,
            CurrentStageId = summary.CurrentStageId,
            CurrentStageName = summary.CurrentStageName,
            RequiresLegalReview = summary.RequiresLegalReview,
            DocumentsTotal = summary.DocumentsTotal,
            DocumentsVerified = summary.DocumentsVerified,
            TasksTotal = summary.TasksTotal,
            TasksCompleted = summary.TasksCompleted,
            CreatedAt = summary.CreatedAt,
            UpdatedAt = summary.UpdatedAt,
            Documents = p.Documents
                .OrderBy(d => d.StageId).ThenBy(d => d.CreatedAt)
                .Select(d => new SaleProcessDocumentDto { Id = d.Id, StageId = d.StageId, Name = d.Name, IsVerified = d.IsVerified, VerifiedAt = d.VerifiedAt })
                .ToList(),
            Tasks = p.Tasks
                .OrderBy(t => t.CreatedAt)
                .Select(t => new SaleProcessTaskDto { Id = t.Id, Title = t.Title, IsCompleted = t.IsCompleted, CompletedAt = t.CompletedAt })
                .ToList()
        };
    }
}
