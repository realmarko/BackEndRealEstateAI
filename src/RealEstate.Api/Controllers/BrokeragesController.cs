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
[Route("api/brokerages")]
public class BrokeragesController : ControllerBase
{
    private readonly RealEstateDbContext _db;
    // Agent/Brokerage live in RealEstateDbContext; Listing lives in ApplicationDbContext — same
    // cross-context split as AgentsController/ListingsController, so a brokerage's listings
    // count takes two round trips (agent UserIds, then a listings count by those ids).
    private readonly ApplicationDbContext _listingsDb;
    private readonly IPhotoUploadService _photoUploadService;

    public BrokeragesController(RealEstateDbContext db, ApplicationDbContext listingsDb, IPhotoUploadService photoUploadService)
    {
        _db = db;
        _listingsDb = listingsDb;
        _photoUploadService = photoUploadService;
    }

    // GET /api/brokerages?search=comey — powers the agent signup autocomplete. Deliberately
    // lightweight (bare names only) — the public /inmobiliarias directory uses Directory() below.
    [HttpGet]
    public async Task<ActionResult<List<string>>> Search([FromQuery] string? search)
    {
        var query = _db.Brokerages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Name.ToLower().Contains(search!.ToLower()));

        var names = await query
            .OrderBy(b => b.Name)
            .Select(b => b.Name)
            .Take(50)
            .ToListAsync();

        return Ok(names);
    }

    // GET /api/brokerages/directory?name=&state=&city=&page=&pageSize= — the public
    // /inmobiliarias listing page: full profiles + agent/listing counts, paginated.
    [HttpGet("directory")]
    public async Task<ActionResult<PagedResult<BrokerageDto>>> Directory([FromQuery] BrokerageDirectoryQuery q)
    {
        var callerBrokerageId = await GetCallerBrokerageIdAsync();

        var query = _db.Brokerages.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Name))
            query = query.Where(b => b.Name.ToLower().Contains(q.Name!.ToLower()));
        if (!string.IsNullOrWhiteSpace(q.State))
            query = query.Where(b => b.State != null && b.State.ToLower() == q.State!.ToLower());
        if (!string.IsNullOrWhiteSpace(q.City))
            query = query.Where(b => b.City != null && b.City.ToLower() == q.City!.ToLower());

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        var pageItems = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectionSelector)
            .ToListAsync();

        var items = await AttachListingCountsAsync(pageItems, callerBrokerageId);

        return Ok(new PagedResult<BrokerageDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    // GET /api/brokerages/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BrokerageDto>> GetById(int id)
    {
        var callerBrokerageId = await GetCallerBrokerageIdAsync();

        var item = await _db.Brokerages
            .Where(b => b.Id == id)
            .Select(ProjectionSelector)
            .FirstOrDefaultAsync();

        if (item is null) return NotFound();

        var dtos = await AttachListingCountsAsync(new List<BrokerageProjection> { item }, callerBrokerageId);
        return Ok(dtos[0]);
    }

    // GET /api/brokerages/mine — the caller's own agency, so the "edit my agency" entry point
    // doesn't need to already know its id. 404 if the agent is independent or has no profile yet.
    [Authorize(Roles = "Agent")]
    [HttpGet("mine")]
    public async Task<ActionResult<BrokerageDto>> Mine()
    {
        var userId = User.GetUserId();
        var brokerageId = await _db.Agents.Where(a => a.UserId == userId).Select(a => a.BrokerageId).FirstOrDefaultAsync();
        return brokerageId is null ? NotFound() : await GetById(brokerageId.Value);
    }

    // PUT /api/brokerages/5 — only an agent who belongs to this brokerage may edit its public
    // profile. Name itself is never editable here — it stays owned by the signup/edit-profile
    // resolve-or-create flow in AgentsController, so two agents at the same brokerage can't fork
    // the catalog by renaming it out from under each other.
    [Authorize(Roles = "Agent")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BrokerageDto>> Update(int id, [FromForm] UpdateBrokerageProfileDto dto)
    {
        var userId = User.GetUserId();
        var isMember = await _db.Agents.AnyAsync(a => a.UserId == userId && a.BrokerageId == id);
        if (!isMember) return Forbid();

        var brokerage = await _db.Brokerages.FindAsync(id);
        if (brokerage is null) return NotFound();

        if (dto.Logo is not null)
        {
            var result = await _photoUploadService.UploadAsync(dto.Logo, $"brokerages/{id}");
            if (result.ErrorKind is not null) return result.ErrorKind.Value.ToActionResult(this);
            brokerage.LogoUrl = result.Urls.FirstOrDefault();
        }

        brokerage.State = dto.State;
        brokerage.City = dto.City;
        brokerage.Website = dto.Website;
        brokerage.Description = dto.Description;
        brokerage.FacebookUrl = dto.FacebookUrl;
        brokerage.InstagramUrl = dto.InstagramUrl;

        await _db.SaveChangesAsync();

        return await GetById(id);
    }

    private async Task<int?> GetCallerBrokerageIdAsync()
    {
        var currentUserId = User.TryGetUserId();
        if (currentUserId is null) return null;
        return await _db.Agents.Where(a => a.UserId == currentUserId).Select(a => a.BrokerageId).FirstOrDefaultAsync();
    }

    private static readonly System.Linq.Expressions.Expression<Func<Brokerage, BrokerageProjection>> ProjectionSelector = b => new BrokerageProjection
    {
        Id = b.Id,
        Name = b.Name,
        LogoUrl = b.LogoUrl,
        State = b.State,
        City = b.City,
        Website = b.Website,
        Description = b.Description,
        FacebookUrl = b.FacebookUrl,
        InstagramUrl = b.InstagramUrl,
        AgentsCount = b.Agents.Count,
        AgentUserIds = b.Agents.Where(a => a.UserId != null).Select(a => a.UserId!.Value).ToList()
    };

    private sealed class BrokerageProjection
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? Website { get; set; }
        public string? Description { get; set; }
        public string? FacebookUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public int AgentsCount { get; set; }
        public List<Guid> AgentUserIds { get; set; } = new();
    }

    // Real listing counts come from Listing (ApplicationDbContext), matched by each brokerage's
    // agents' UserIds — summed in one grouped query across the whole page instead of per-brokerage,
    // since Brokerage and Listing live in separate DbContexts (no SQL join possible).
    private async Task<List<BrokerageDto>> AttachListingCountsAsync(List<BrokerageProjection> pageItems, int? callerBrokerageId)
    {
        var allUserIds = pageItems.SelectMany(b => b.AgentUserIds).Distinct().ToList();
        var listingCounts = allUserIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _listingsDb.Listings
                .Where(l => allUserIds.Contains(l.OwnerId) && l.Status != ListingStatus.Removed)
                .GroupBy(l => l.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.OwnerId, g => g.Count);

        return pageItems.Select(b => new BrokerageDto
        {
            Id = b.Id,
            Name = b.Name,
            LogoUrl = b.LogoUrl,
            State = b.State,
            City = b.City,
            Website = b.Website,
            Description = b.Description,
            FacebookUrl = b.FacebookUrl,
            InstagramUrl = b.InstagramUrl,
            AgentsCount = b.AgentsCount,
            ListingsCount = b.AgentUserIds.Sum(uid => listingCounts.GetValueOrDefault(uid, 0)),
            CanEdit = callerBrokerageId.HasValue && callerBrokerageId.Value == b.Id
        }).ToList();
    }
}
