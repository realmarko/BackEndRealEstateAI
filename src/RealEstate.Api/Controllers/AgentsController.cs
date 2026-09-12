using System.Security.Claims;
using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    private readonly RealEstateDbContext _db;
    private readonly IS3UploadService _s3Service;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(RealEstateDbContext db, IS3UploadService s3Service, ILogger<AgentsController> logger)
    {
        _db = db;
        _s3Service = s3Service;
        _logger = logger;
    }

    // GET /api/agents?name=smith
    [HttpGet]
    public async Task<ActionResult<PagedResult<AgentDto>>> Search([FromQuery] AgentSearchQuery q)
    {
        var query = _db.Agents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Name))
            query = query.Where(a => a.Name.ToLower().Contains(q.Name!.ToLower()));

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        // Projects PropertiesCount directly (a SQL COUNT subquery) instead of Include()-ing
        // every Property row just to read .Count in memory.
        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AgentDto
            {
                Id = a.Id,
                Name = a.Name,
                Email = a.Email,
                Phone = a.Phone,
                Company = a.Company,
                PhotoUrl = a.PhotoUrl,
                PropertiesCount = a.Properties.Count
            })
            .ToListAsync();

        return Ok(new PagedResult<AgentDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDto>> GetById(int id)
    {
        var agent = await _db.Agents
            .Where(a => a.Id == id)
            .Select(a => new AgentDto
            {
                Id = a.Id,
                Name = a.Name,
                Email = a.Email,
                Phone = a.Phone,
                Company = a.Company,
                PhotoUrl = a.PhotoUrl,
                PropertiesCount = a.Properties.Count
            })
            .FirstOrDefaultAsync();

        return agent is null ? NotFound() : Ok(agent);
    }

    // Completes the agent profile for the currently logged-in user (registered with the Agent role)
    [Authorize(Roles = "Agent")]
    [HttpPost]
    public async Task<ActionResult<AgentDto>> Create([FromForm] CreateAgentDto dto)
    {
        var userId = User.GetUserId();
        if (await _db.Agents.AnyAsync(a => a.UserId == userId))
            return Conflict(new { message = "An agent profile already exists for this account." });

        string? photoUrl = null;
        if (dto.Photo is not null)
        {
            if (!AllowedPhotoTypes.Contains(dto.Photo.ContentType))
                return BadRequest(new { message = "Photo must be a JPEG, PNG, or WEBP image." });
            if (dto.Photo.Length > MaxPhotoBytes)
                return BadRequest(new { message = "Photo must be 5 MB or smaller." });

            try
            {
                photoUrl = await _s3Service.UploadFileAsync(dto.Photo, $"agents/{userId}");
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Failed to upload agent photo to S3 for user {UserId}", userId);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { message = "Could not upload the photo right now. Please try again." });
            }
        }

        var agent = new Agent
        {
            UserId = userId,
            Name = $"{User.FindFirstValue("firstName")} {User.FindFirstValue("lastName")}".Trim(),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Phone = dto.Phone,
            Company = dto.Company,
            PhotoUrl = photoUrl
        };

        _db.Agents.Add(agent);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate submit raced past the AnyAsync check above and hit the
            // unique index on UserId — treat it the same as the check finding it first.
            return Conflict(new { message = "An agent profile already exists for this account." });
        }

        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, ToDto(agent));
    }

    private static AgentDto ToDto(Agent a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Email = a.Email,
        Phone = a.Phone,
        Company = a.Company,
        PhotoUrl = a.PhotoUrl,
        PropertiesCount = a.Properties.Count
    };
}
