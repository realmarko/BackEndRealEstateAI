using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    // Short TTLs (not the 24h used for truly static reference data elsewhere) — agent/listing
    // counts and profile edits are real, if infrequent, changes that should show up reasonably
    // soon. Update() explicitly evicts ProfileCacheKey(id) on every edit, so an agent always sees
    // their own change immediately regardless of this TTL; everyone else's view catches up within it.
    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DirectoryCacheDuration = TimeSpan.FromMinutes(2);

    private readonly RealEstateDbContext _db;
    // Agent/Brokerage live in RealEstateDbContext; Listing lives in ApplicationDbContext — same
    // cross-context split as AgentsController/ListingsController, so a brokerage's listings
    // count takes two round trips (agent UserIds, then a listings count by those ids).
    private readonly ApplicationDbContext _listingsDb;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IMemoryCache _cache;

    public BrokeragesController(RealEstateDbContext db, ApplicationDbContext listingsDb, IPhotoUploadService photoUploadService, IMemoryCache cache)
    {
        _db = db;
        _listingsDb = listingsDb;
        _photoUploadService = photoUploadService;
        _cache = cache;
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
    //
    // The expensive part (querying+counting a page of brokerages) is cached as-is — it has no
    // per-caller data in it. CanEdit is computed fresh on every request, after the cache lookup,
    // and never written back into the cached objects (see ToPublicDto) — a cached page is shared
    // across every visitor regardless of who's logged in, so nothing caller-specific may leak
    // into it.
    [HttpGet("directory")]
    public async Task<ActionResult<PagedResult<BrokerageDto>>> Directory([FromQuery] BrokerageDirectoryQuery q)
    {
        var callerBrokerageId = await GetCallerBrokerageIdAsync();

        var cacheKey = DirectoryCacheKey(q);
        var cachedPage = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DirectoryCacheDuration;

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

            var profiles = await BuildProfilesAsync(pageItems);

            return new CachedDirectoryPage { Profiles = profiles, Page = page, PageSize = pageSize, TotalCount = totalCount };
        });

        var items = cachedPage!.Profiles
            .Select(p => ToPublicDto(p, callerBrokerageId.HasValue && callerBrokerageId.Value == p.Id))
            .ToList();

        return Ok(new PagedResult<BrokerageDto>
        {
            Items = items,
            Page = cachedPage.Page,
            PageSize = cachedPage.PageSize,
            TotalCount = cachedPage.TotalCount
        });
    }

    // GET /api/brokerages/5 — same cache-then-overlay-CanEdit shape as Directory above.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BrokerageDto>> GetById(int id)
    {
        var callerBrokerageId = await GetCallerBrokerageIdAsync();

        var cachedProfile = await _cache.GetOrCreateAsync(ProfileCacheKey(id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProfileCacheDuration;

            var item = await _db.Brokerages
                .Where(b => b.Id == id)
                .Select(ProjectionSelector)
                .FirstOrDefaultAsync();

            if (item is null) return null;

            var profiles = await BuildProfilesAsync(new List<BrokerageProjection> { item });
            return profiles[0];
        });

        if (cachedProfile is null) return NotFound();

        return Ok(ToPublicDto(cachedProfile, callerBrokerageId.HasValue && callerBrokerageId.Value == cachedProfile.Id));
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

        // Without this, the agent who just edited their own profile would keep seeing the old
        // cached version for up to ProfileCacheDuration — worse than no cache at all. Directory
        // pages aren't individually evicted (there's no cheap way to know which cached pages
        // might include this brokerage) and simply catch up within DirectoryCacheDuration.
        _cache.Remove(ProfileCacheKey(id));

        return await GetById(id);
    }

    private async Task<int?> GetCallerBrokerageIdAsync()
    {
        var currentUserId = User.TryGetUserId();
        if (currentUserId is null) return null;
        return await _db.Agents.Where(a => a.UserId == currentUserId).Select(a => a.BrokerageId).FirstOrDefaultAsync();
    }

    private static string ProfileCacheKey(int id) => $"brokerage-profile:{id}";

    private static string DirectoryCacheKey(BrokerageDirectoryQuery q) =>
        $"brokerage-directory:{q.Name?.Trim().ToLowerInvariant()}:{q.State?.Trim().ToLowerInvariant()}:{q.City?.Trim().ToLowerInvariant()}:{q.Page}:{q.PageSize}";

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

    // Cacheable shape: every brokerage profile field that's the same for every viewer. Deliberately
    // has no CanEdit (or anything else caller-specific) — there is no field here to accidentally
    // leak between callers, by construction, not just by convention.
    private sealed class CachedBrokerageProfile
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
        public int ListingsCount { get; set; }
    }

    private sealed class CachedDirectoryPage
    {
        public List<CachedBrokerageProfile> Profiles { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // Builds the cacheable (caller-independent) profile shape, including the cross-context
    // listings-count join — this is the expensive part Directory/GetById cache the result of.
    private async Task<List<CachedBrokerageProfile>> BuildProfilesAsync(List<BrokerageProjection> pageItems)
    {
        var allUserIds = pageItems.SelectMany(b => b.AgentUserIds).Distinct().ToList();
        var listingCounts = allUserIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _listingsDb.Listings
                .Where(l => allUserIds.Contains(l.OwnerId) && l.Status != ListingStatus.Removed)
                .GroupBy(l => l.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.OwnerId, g => g.Count);

        return pageItems.Select(b => new CachedBrokerageProfile
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
            ListingsCount = b.AgentUserIds.Sum(uid => listingCounts.GetValueOrDefault(uid, 0))
        }).ToList();
    }

    // Always builds a brand-new BrokerageDto rather than mutating the cached CachedBrokerageProfile
    // in place — IMemoryCache hands back the same shared instance to every caller, so writing
    // CanEdit onto it directly would leak one caller's edit permission into every other caller's
    // response until the cache entry expired.
    private static BrokerageDto ToPublicDto(CachedBrokerageProfile cached, bool canEdit) => new()
    {
        Id = cached.Id,
        Name = cached.Name,
        LogoUrl = cached.LogoUrl,
        State = cached.State,
        City = cached.City,
        Website = cached.Website,
        Description = cached.Description,
        FacebookUrl = cached.FacebookUrl,
        InstagramUrl = cached.InstagramUrl,
        AgentsCount = cached.AgentsCount,
        ListingsCount = cached.ListingsCount,
        CanEdit = canEdit
    };
}
