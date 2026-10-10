using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
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

    // POST /api/brokerages — an admin pinning a brokerage's physical office on the map (see the
    // "Agregar inmobiliaria" tool on /map). Creates the row directly, unlike
    // AgentsController.ResolveOrCreateBrokerageAsync's name-only resolve-or-create during agent
    // signup — there's no dedup concern here since an admin is placing one specific, deliberate
    // pin, not matching free-text company names typed by different agents.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BrokerageDto>> Create([FromForm] CreateBrokerageDto dto)
    {
        var brokerage = new Brokerage
        {
            Name = dto.Name,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Address = dto.Address,
            Phone = dto.Phone,
            Email = dto.Email,
            WorkingHours = dto.WorkingHours,
            Website = dto.Website
        };

        if (dto.Photo is not null)
        {
            var result = await _photoUploadService.UploadAsync(dto.Photo, $"brokerages/new-{Guid.NewGuid()}");
            if (result.ErrorKind is not null) return result.ErrorKind.Value.ToActionResult(this);
            brokerage.LogoUrl = result.Urls.FirstOrDefault();
        }

        _db.Brokerages.Add(brokerage);
        await _db.SaveChangesAsync();

        return await GetById(brokerage.Id);
    }

    // POST /api/brokerages/import — Admin-only bulk upload for /admin/inmobiliarias. Expected CSV
    // header: nombre,lat,lng,direccion,telefono,email,horario (RFC4180 quoting for fields with
    // embedded commas, e.g. direccion — handled by CsvHelper). Each row is validated and created
    // independently, reported back by row number, rather than all-or-nothing: an admin pasting a
    // spreadsheet export is exactly the kind of input likely to have a typo in one row out of a
    // hundred, and that shouldn't block the other ninety-nine.
    private const long MaxImportFileBytes = 2 * 1024 * 1024;
    private const int MaxImportRows = 1000;

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BrokerageImportResultDto>> Import([FromForm] BrokerageImportDto dto)
    {
        var file = dto.File;
        if (file is null || file.Length == 0) return BadRequest(new { message = "A CSV file is required." });
        if (file.Length > MaxImportFileBytes)
            return BadRequest(new { message = $"The file must be {MaxImportFileBytes / 1024 / 1024} MB or less." });

        List<BrokerageCsvRow> rows;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            rows = csv.GetRecords<BrokerageCsvRow>().ToList();
        }
        catch (Exception ex) when (ex is CsvHelperException or IOException)
        {
            return BadRequest(new { message = "Could not read the CSV file — check its format and header row (nombre,lat,lng,direccion,telefono,email,horario)." });
        }

        if (rows.Count > MaxImportRows)
            return BadRequest(new { message = $"A single import is limited to {MaxImportRows} rows." });

        var result = new BrokerageImportResultDto();
        var toCreate = new List<Brokerage>();

        for (var i = 0; i < rows.Count; i++)
        {
            // +1 for 1-based counting, +1 for the header row — matches the line an admin would
            // count in the original file.
            var rowNumber = i + 2;
            var error = ValidateImportRow(rows[i], out var brokerage);
            if (error is not null)
            {
                result.Errors.Add(new BrokerageImportRowErrorDto { RowNumber = rowNumber, Message = error });
                continue;
            }
            toCreate.Add(brokerage!);
        }

        if (toCreate.Count > 0)
        {
            _db.Brokerages.AddRange(toCreate);
            await _db.SaveChangesAsync();
        }

        result.CreatedCount = toCreate.Count;
        return Ok(result);
    }

    private static string? ValidateImportRow(BrokerageCsvRow row, out Brokerage? brokerage)
    {
        brokerage = null;

        var name = row.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return "nombre is required.";
        if (name.Length > 200) return "nombre must be 200 characters or less.";

        if (!double.TryParse(row.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            return "lat must be a number.";
        if (!double.TryParse(row.Lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            return "lng must be a number.";
        var geoError = GeoValidation.ValidateLatLng(lat, lng);
        if (geoError is not null) return geoError;

        var address = row.Direccion?.Trim();
        var phone = row.Telefono?.Trim();
        var email = row.Email?.Trim();
        var workingHours = row.Horario?.Trim();

        if (address is { Length: > 300 }) return "direccion must be 300 characters or less.";
        if (phone is { Length: > 30 }) return "telefono must be 30 characters or less.";
        if (email is { Length: > 320 }) return "email must be 320 characters or less.";
        if (!string.IsNullOrEmpty(email) && !new EmailAddressAttribute().IsValid(email)) return "email is not a valid email address.";
        if (workingHours is { Length: > 200 }) return "horario must be 200 characters or less.";

        brokerage = new Brokerage
        {
            Name = name,
            Latitude = lat,
            Longitude = lng,
            Address = string.IsNullOrWhiteSpace(address) ? null : address,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            WorkingHours = string.IsNullOrWhiteSpace(workingHours) ? null : workingHours
        };
        return null;
    }

    // Lat/Lng are read as strings (not double) so a malformed value becomes a row-level
    // ValidateImportRow error message instead of a CsvHelper TypeConverterException thrown mid-
    // enumeration — which would abort GetRecords<T>() for every row after it, not just the bad one.
    private sealed class BrokerageCsvRow
    {
        [Name("nombre")] public string? Nombre { get; set; }
        [Name("lat")] public string? Lat { get; set; }
        [Name("lng")] public string? Lng { get; set; }
        [Name("direccion")] public string? Direccion { get; set; }
        [Name("telefono")] public string? Telefono { get; set; }
        [Name("email")] public string? Email { get; set; }
        [Name("horario")] public string? Horario { get; set; }
    }

    // GET /api/brokerages/map — every brokerage an admin has pinned with a location, for the
    // map's own marker layer. Deliberately excludes the (much more common) rows that only have a
    // Name resolved from agent signup — those have no Latitude/Longitude to plot. Uncached and
    // unpaginated: there's no listing-page-scale traffic driving this one (only /map loads it,
    // once per page load), and the row count is bounded by how many an admin has manually added.
    [HttpGet("map")]
    public async Task<ActionResult<List<BrokerageMapItemDto>>> Map()
    {
        var items = await _db.Brokerages
            .Where(b => b.Latitude != null && b.Longitude != null)
            .Select(b => new BrokerageMapItemDto
            {
                Id = b.Id,
                Name = b.Name,
                LogoUrl = b.LogoUrl,
                Latitude = b.Latitude!.Value,
                Longitude = b.Longitude!.Value
            })
            .ToListAsync();

        return Ok(items);
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

    // GET /api/brokerages/by-slug/some_brokerage_name — resolves a brokerage the same way the
    // frontend builds every link to one now (ToSlug(name) in brokerage-api.adapter.ts: spaces
    // replaced with underscores), so a visitor never sees the numeric id in the URL. Name has no
    // uniqueness constraint, so two brokerages could in theory share a slug — the oldest (lowest
    // id) wins; not actively guarded against since brokerage rows are admin/agent-curated and
    // collisions are expected to be rare enough not to need a disambiguation scheme yet.
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<BrokerageDto>> GetBySlug(string slug)
    {
        var id = await _db.Brokerages
            .Where(b => b.Name.Replace(" ", "_") == slug)
            .OrderBy(b => b.Id)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

        if (id is null) return NotFound();
        return await GetById(id.Value);
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

    // PUT /api/brokerages/5/admin — Admin-only, every field (see AdminUpdateBrokerageDto). Unlike
    // Update() above, there's no membership check: this is how an admin-pinned brokerage (which
    // has no member agent at all) gets edited, and also lets an admin fix/relocate any brokerage
    // regardless of who it's resolved from.
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}/admin")]
    public async Task<ActionResult<BrokerageDto>> AdminUpdate(int id, [FromForm] AdminUpdateBrokerageDto dto)
    {
        if (dto.Latitude.HasValue != dto.Longitude.HasValue)
            return BadRequest(new { message = "Latitude and Longitude must both be set, or both left empty." });
        if (dto.Latitude.HasValue)
        {
            var geoError = GeoValidation.ValidateLatLng(dto.Latitude.Value, dto.Longitude!.Value);
            if (geoError is not null) return BadRequest(new { message = geoError });
        }

        var brokerage = await _db.Brokerages.FindAsync(id);
        if (brokerage is null) return NotFound();

        if (dto.Logo is not null)
        {
            var result = await _photoUploadService.UploadAsync(dto.Logo, $"brokerages/{id}");
            if (result.ErrorKind is not null) return result.ErrorKind.Value.ToActionResult(this);
            brokerage.LogoUrl = result.Urls.FirstOrDefault();
        }

        brokerage.Name = dto.Name;
        brokerage.Latitude = dto.Latitude;
        brokerage.Longitude = dto.Longitude;
        brokerage.Address = dto.Address;
        brokerage.Phone = dto.Phone;
        brokerage.Email = dto.Email;
        brokerage.WorkingHours = dto.WorkingHours;
        brokerage.State = dto.State;
        brokerage.City = dto.City;
        brokerage.Website = dto.Website;
        brokerage.Description = dto.Description;
        brokerage.FacebookUrl = dto.FacebookUrl;
        brokerage.InstagramUrl = dto.InstagramUrl;

        await _db.SaveChangesAsync();

        // Same reasoning as Update() above — an admin editing a brokerage should see their own
        // change immediately, not the stale cached profile for up to ProfileCacheDuration.
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
        Latitude = b.Latitude,
        Longitude = b.Longitude,
        Address = b.Address,
        Phone = b.Phone,
        Email = b.Email,
        WorkingHours = b.WorkingHours,
        AgentsCount = b.Agents.Count(a => !a.IsDeleted),
        AgentUserIds = b.Agents.Where(a => !a.IsDeleted && a.UserId != null).Select(a => a.UserId!.Value).ToList()
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
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? WorkingHours { get; set; }
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
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? WorkingHours { get; set; }
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
            Latitude = b.Latitude,
            Longitude = b.Longitude,
            Address = b.Address,
            Phone = b.Phone,
            Email = b.Email,
            WorkingHours = b.WorkingHours,
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
        Latitude = cached.Latitude,
        Longitude = cached.Longitude,
        Address = cached.Address,
        Phone = cached.Phone,
        Email = cached.Email,
        WorkingHours = cached.WorkingHours,
        AgentsCount = cached.AgentsCount,
        ListingsCount = cached.ListingsCount,
        CanEdit = canEdit
    };
}
