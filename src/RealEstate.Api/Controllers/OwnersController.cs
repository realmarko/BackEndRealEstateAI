using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

// Public directory for plain property owners (role Owner, NOT also Agent — an Agent already has
// a profile in /api/agents, so letting the same person opt into both would duplicate them across
// two directories for the same listings). Mirrors AgentsController's shape throughout, trimmed:
// no Brokerage/Specialties/Reviews, since those are agent-specific concepts a plain owner has no
// equivalent of.
[ApiController]
[Route("api/owners")]
public class OwnersController : ControllerBase
{
    // Same TTLs/reasoning as AgentsController's own constants.
    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromMinutes(2);
    private const string SearchCacheVersionKey = "owner-search-cache-version";

    private readonly RealEstateDbContext _db;
    private readonly ApplicationDbContext _listingsDb;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IEmailService _emailService;
    private readonly IOwnerAgentLookupService _ownerAgentLookup;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OwnersController> _logger;

    public OwnersController(
        RealEstateDbContext db,
        ApplicationDbContext listingsDb,
        IPhotoUploadService photoUploadService,
        IEmailService emailService,
        IOwnerAgentLookupService ownerAgentLookup,
        IMemoryCache cache,
        ILogger<OwnersController> logger)
    {
        _db = db;
        _listingsDb = listingsDb;
        _photoUploadService = photoUploadService;
        _emailService = emailService;
        _ownerAgentLookup = ownerAgentLookup;
        _cache = cache;
        _logger = logger;
    }

    // GET /api/owners?name=smith — same cache-then-overlay-IsOwnProfile shape as AgentsController.Search.
    [HttpGet]
    public async Task<ActionResult<PagedResult<OwnerDto>>> Search([FromQuery] OwnerSearchQuery q)
    {
        var currentUserId = User.TryGetUserId();

        var cachedPage = await _cache.GetOrCreateAsync(SearchCacheKey(q), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SearchCacheDuration;

            var query = _db.Owners.Where(o => !o.IsDeleted);

            if (!string.IsNullOrWhiteSpace(q.Name))
                query = query.Where(o => o.Name.ToLower().Contains(q.Name!.ToLower()));

            var totalCount = await query.CountAsync();

            var page = Math.Max(q.Page, 1);
            var pageSize = Math.Clamp(q.PageSize, 1, 100);

            var pageItems = await query
                .OrderBy(o => o.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new { o.Id, o.UserId, o.Name, o.Email, o.Phone, o.PhotoUrl, o.Bio, o.ViewCount })
                .ToListAsync();

            // Same batched-count-across-the-page approach as AgentsController.Search — Owner and
            // Listing live in separate DbContexts, so there's no SQL join to lean on instead.
            var pageUserIds = pageItems.Where(o => o.UserId.HasValue).Select(o => o.UserId!.Value).ToList();
            var listingCounts = await _listingsDb.Listings
                // !HideAgentContact: a listing flagged that way is counted below instead, via
                // ownerContactCounts — needed so a listing matching BOTH paths (e.g. an owner's
                // own, already-claimed listing that also has HideAgentContact set) isn't added
                // into both dictionaries and double-counted. Mirrors CountListingsAsync's single
                // OR'd WHERE clause below, just split into two GroupBys for the batched page query.
                .Where(l => pageUserIds.Contains(l.OwnerId) && l.Status != ListingStatus.Removed && !l.HideAgentContact)
                .GroupBy(l => l.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.OwnerId, g => g.Count);

            // A listing with HideAgentContact set (ListingsController.UpdateOwnerContact) is
            // matched to this directory purely by OwnerContactEmail, not Listing.OwnerId — the
            // real owner often has no account yet (UserId null), and even once claimed,
            // Listing.OwnerId keeps pointing at whichever agent account actually manages it.
            var pageEmails = pageItems
                .Where(o => !string.IsNullOrWhiteSpace(o.Email))
                .Select(o => o.Email.Trim().ToLower())
                .Distinct()
                .ToList();
            var ownerContactCounts = await _listingsDb.Listings
                .Where(l => l.HideAgentContact && l.OwnerContactEmail != null
                    && pageEmails.Contains(l.OwnerContactEmail.ToLower()) && l.Status != ListingStatus.Removed)
                .GroupBy(l => l.OwnerContactEmail!.ToLower())
                .Select(g => new { Email = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Email, g => g.Count);

            var profiles = pageItems.Select(o => new CachedOwnerProfile
            {
                Id = o.Id,
                UserId = o.UserId,
                Name = o.Name,
                Email = o.Email,
                Phone = o.Phone,
                PhotoUrl = o.PhotoUrl,
                Bio = o.Bio,
                PropertiesCount = (o.UserId.HasValue && listingCounts.TryGetValue(o.UserId.Value, out var count) ? count : 0)
                    + (!string.IsNullOrWhiteSpace(o.Email) && ownerContactCounts.TryGetValue(o.Email.Trim().ToLower(), out var ocCount) ? ocCount : 0),
                ViewCount = o.ViewCount
            }).ToList();

            return new CachedOwnerSearchPage { Profiles = profiles, Page = page, PageSize = pageSize, TotalCount = totalCount };
        });

        var isAuthenticated = User.IsAuthenticated();
        var items = cachedPage!.Profiles
            .Select(p => ToPublicDto(p, currentUserId != null && p.UserId == currentUserId, isAuthenticated))
            .ToList();

        return Ok(new PagedResult<OwnerDto>
        {
            Items = items,
            Page = cachedPage.Page,
            PageSize = cachedPage.PageSize,
            TotalCount = cachedPage.TotalCount
        });
    }

    // GET /api/owners/5 — same cache-then-overlay-IsOwnProfile shape as Search above.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OwnerDto>> GetById(int id)
    {
        var currentUserId = User.TryGetUserId();

        var cachedProfile = await _cache.GetOrCreateAsync(ProfileCacheKey(id), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ProfileCacheDuration;

            var owner = await _db.Owners
                .Where(o => o.Id == id && !o.IsDeleted)
                .Select(o => new { o.Id, o.UserId, o.Name, o.Email, o.Phone, o.PhotoUrl, o.Bio, o.ViewCount })
                .FirstOrDefaultAsync();

            if (owner is null) return null;

            return new CachedOwnerProfile
            {
                Id = owner.Id,
                UserId = owner.UserId,
                Name = owner.Name,
                Email = owner.Email,
                Phone = owner.Phone,
                PhotoUrl = owner.PhotoUrl,
                Bio = owner.Bio,
                PropertiesCount = await CountListingsAsync(owner.UserId, owner.Email),
                ViewCount = owner.ViewCount
            };
        });

        if (cachedProfile is null) return NotFound();

        var dto = ToPublicDto(cachedProfile, currentUserId != null && cachedProfile.UserId == currentUserId, User.IsAuthenticated());
        // Read live rather than trusting the (up to 5-minute-stale) cached value — same reasoning
        // as AgentsController.GetById.
        dto.ViewCount = await _db.Owners.Where(o => o.Id == id).Select(o => o.ViewCount).FirstOrDefaultAsync();

        return Ok(dto);
    }

    // GET /api/owners/by-slug/JuanPerez — same toSlug(name) convention as
    // AgentsController.GetBySlug (spaces removed entirely): the frontend's owner-api.adapter.ts
    // builds every link with the same transform, so a visitor never sees the numeric id.
    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<OwnerDto>> GetBySlug(string slug)
    {
        var id = await _db.Owners
            .Where(o => !o.IsDeleted && o.Name.Replace(" ", "") == slug)
            .OrderBy(o => o.Id)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync();

        if (id is null) return NotFound();
        return await GetById(id.Value);
    }

    // Fire-and-forget from the detail page on load — same reasoning as AgentsController.RecordView.
    [EnableRateLimiting("view-count")]
    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> RecordView(int id)
    {
        var rows = await _db.Owners
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ViewCount, o => o.ViewCount + 1));

        return rows == 0 ? NotFound() : NoContent();
    }

    // Counts both paths a listing can belong to this owner: Listing.OwnerId == userId (the
    // owner manages it directly under their own account) and the HideAgentContact/email match
    // (ListingsController.UpdateOwnerContact set this directory as the listing's real owner,
    // independent of who technically holds edit rights — see Search's own comment above).
    private async Task<int> CountListingsAsync(Guid? userId, string? email)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower();
        return await _listingsDb.Listings.CountAsync(l =>
            l.Status != ListingStatus.Removed &&
            ((userId != null && l.OwnerId == userId) ||
             (normalizedEmail != null && l.HideAgentContact && l.OwnerContactEmail != null && l.OwnerContactEmail.ToLower() == normalizedEmail)));
    }

    // GET /api/owners/5/listings — same two-path match as CountListingsAsync above.
    [HttpGet("{id:int}/listings")]
    public async Task<ActionResult<List<ListingDto>>> GetListings(int id)
    {
        var owner = await _db.Owners.Where(o => o.Id == id).Select(o => new { o.UserId, o.Email }).FirstOrDefaultAsync();
        if (owner is null) return Ok(new List<ListingDto>());

        var normalizedEmail = string.IsNullOrWhiteSpace(owner.Email) ? null : owner.Email.Trim().ToLower();
        var listings = await _listingsDb.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.Status != ListingStatus.Removed &&
                ((owner.UserId != null && l.OwnerId == owner.UserId) ||
                 (normalizedEmail != null && l.HideAgentContact && l.OwnerContactEmail != null && l.OwnerContactEmail.ToLower() == normalizedEmail)))
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(l => l.ToDto(User.IsAuthenticated())));
    }

    // Completes the owner profile for the currently logged-in user. Requires the Owner role (set
    // at registration/role-upgrade) but explicitly rejects anyone who also has the Agent role —
    // that person already gets a public profile via /api/agents, and letting them create a second
    // one here would show the same listings twice across two directories.
    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<ActionResult<OwnerDto>> Create([FromForm] CreateOwnerDto dto)
    {
        if (User.IsInRole("Agent"))
            return BadRequest(new { message = "You already have an agent profile — use that instead of a separate owner profile." });

        var userId = User.GetUserId();
        if (await _db.Owners.AnyAsync(o => o.UserId == userId))
            return Conflict(new { message = "An owner profile already exists for this account." });

        string? photoUrl = null;
        if (dto.Photo is not null)
        {
            var (uploadedUrl, uploadError) = await TryUploadPhotoAsync(dto.Photo, $"owners/{userId}");
            if (uploadError is not null) return uploadError;
            photoUrl = uploadedUrl;
        }

        var owner = new Owner
        {
            UserId = userId,
            Name = $"{User.FindFirstValue("firstName")} {User.FindFirstValue("lastName")}".Trim(),
            Email = User.TryGetEmail() ?? string.Empty,
            Phone = dto.Phone,
            PhotoUrl = photoUrl,
            Bio = dto.Bio
        };

        _db.Owners.Add(owner);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate submit raced past the AnyAsync check above and hit the
            // unique index on UserId — treat it the same as the check finding it first.
            return Conflict(new { message = "An owner profile already exists for this account." });
        }

        BumpSearchCacheVersion();
        // A fresh profile means ListingsController's per-owner Company/PhotoUrl cache (which
        // checks Owners as a fallback when there's no matching Agent — see
        // OwnerAgentLookupService) may have already cached "no profile" (null, null) for this
        // user from an earlier listing-page view. Evict so their existing listings' cards pick up
        // the new photo immediately instead of waiting out that cache's own TTL.
        _ownerAgentLookup.Evict(userId);
        return CreatedAtAction(nameof(GetById), new { id = owner.Id }, ToDto(owner, isOwnProfile: true, propertiesCount: await CountListingsAsync(userId, owner.Email)));
    }

    // Returns the current user's own owner profile — same reasoning as AgentsController.GetMine
    // (bypasses the cache; this is the source of truth the edit form loads from).
    [Authorize(Roles = "Owner")]
    [HttpGet("me")]
    public async Task<ActionResult<OwnerDto>> GetMine()
    {
        var userId = User.GetUserId();
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == userId);
        return owner is null ? NotFound() : Ok(ToDto(owner, isOwnProfile: true, propertiesCount: await CountListingsAsync(userId, owner.Email)));
    }

    // Updates the current user's own owner profile — same reasoning as AgentsController.UpdateMine
    // (the row to update is found by UserId from the token, never a client-supplied id).
    [Authorize(Roles = "Owner")]
    [HttpPut("me")]
    public async Task<ActionResult<OwnerDto>> UpdateMine([FromForm] CreateOwnerDto dto)
    {
        var userId = User.GetUserId();
        var owner = await _db.Owners.FirstOrDefaultAsync(o => o.UserId == userId);
        if (owner is null) return NotFound();

        if (dto.Photo is not null)
        {
            var (uploadedUrl, uploadError) = await TryUploadPhotoAsync(dto.Photo, $"owners/{userId}");
            if (uploadError is not null) return uploadError;
            owner.PhotoUrl = uploadedUrl;
        }

        owner.Phone = dto.Phone;
        owner.Bio = dto.Bio;

        await _db.SaveChangesAsync();

        _cache.Remove(ProfileCacheKey(owner.Id));
        BumpSearchCacheVersion();
        // PhotoUrl just changed — same reasoning as AgentsController.UpdateMine's own eviction.
        if (owner.UserId.HasValue)
            _ownerAgentLookup.Evict(owner.UserId.Value);

        return Ok(ToDto(owner, isOwnProfile: true, propertiesCount: await CountListingsAsync(userId, owner.Email)));
    }

    // Validates and uploads a photo to S3 — same helper shape as AgentsController's own.
    private async Task<(string? Url, ActionResult? Error)> TryUploadPhotoAsync(IFormFile photo, string keyPrefix)
    {
        var result = await _photoUploadService.UploadAsync(photo, keyPrefix);
        if (result.ErrorKind is not null) return (null, result.ErrorKind.Value.ToActionResult(this));

        return (result.Urls.FirstOrDefault(), null);
    }

    // Admin-only soft delete — same reasoning as AgentsController.Delete.
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var owner = await _db.Owners.FindAsync(id);
        if (owner is null || owner.IsDeleted) return NotFound();

        owner.IsDeleted = true;
        owner.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _cache.Remove(ProfileCacheKey(id));
        BumpSearchCacheVersion();

        return NoContent();
    }

    // Sends the visitor's message straight to the owner's email — same reasoning as
    // AgentsController.Contact (anonymous, rate-limited, no DB record kept).
    [EnableRateLimiting("contact")]
    [HttpPost("{id:int}/contact")]
    public async Task<IActionResult> Contact(int id, ContactOwnerDto dto)
    {
        var owner = await _db.Owners.FindAsync(id);
        if (owner is null) return NotFound();

        if (string.IsNullOrWhiteSpace(owner.Email))
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "This owner can't be reached by email right now." });

        var subject = $"New inquiry from {StripControlCharacters(dto.Name)} via RealEstateApp";
        var body =
            $"You have a new message from a potential client on RealEstateApp.\n\n" +
            $"Name: {dto.Name}\n" +
            $"Phone: {dto.Phone}\n" +
            $"Email: {dto.Email}\n\n" +
            $"Message:\n{dto.Message}";

        try
        {
            await _emailService.SendAsync(owner.Email, owner.Name, subject, body);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to send contact email to owner {OwnerId}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not send the message right now. Please try again." });
        }

        return NoContent();
    }

    private static string StripControlCharacters(string value) =>
        new(value.Where(c => !char.IsControl(c)).ToArray());

    private static string ProfileCacheKey(int id) => $"owner-profile:{id}";

    private string SearchCacheKey(OwnerSearchQuery q) =>
        $"owner-search:v{CurrentSearchCacheVersion()}:{q.Name?.Trim().ToLowerInvariant()}:{q.Page}:{q.PageSize}";

    private int CurrentSearchCacheVersion() =>
        _cache.GetOrCreate(SearchCacheVersionKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return 0;
        });

    private void BumpSearchCacheVersion() =>
        _cache.Set(SearchCacheVersionKey, CurrentSearchCacheVersion() + 1,
            new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });

    // Cacheable shape: every owner profile field that's the same for every viewer, plus UserId
    // (needed to compute IsOwnProfile after a cache hit — never itself exposed on OwnerDto).
    private sealed class CachedOwnerProfile
    {
        public int Id { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string? Bio { get; set; }
        public int PropertiesCount { get; set; }
        public int ViewCount { get; set; }
    }

    private sealed class CachedOwnerSearchPage
    {
        public List<CachedOwnerProfile> Profiles { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    // Same redaction constants/reasoning as AgentsController — the real value is never sent to an
    // anonymous caller, not just hidden by CSS.
    private const string RedactedEmail = "•••••••@•••••.com";
    private const string RedactedPhone = "••• ••• ••••";

    private static OwnerDto ToPublicDto(CachedOwnerProfile cached, bool isOwnProfile, bool isAuthenticated) => new()
    {
        Id = cached.Id,
        IsOwnProfile = isOwnProfile,
        Name = cached.Name,
        Email = isAuthenticated ? cached.Email : RedactedEmail,
        Phone = isAuthenticated ? cached.Phone : RedactedPhone,
        PhotoUrl = cached.PhotoUrl,
        Bio = cached.Bio,
        PropertiesCount = cached.PropertiesCount,
        ViewCount = cached.ViewCount
    };

    private static OwnerDto ToDto(Owner o, bool isOwnProfile, int propertiesCount) => new()
    {
        Id = o.Id,
        IsOwnProfile = isOwnProfile,
        Name = o.Name,
        Email = o.Email,
        Phone = o.Phone,
        PhotoUrl = o.PhotoUrl,
        Bio = o.Bio,
        PropertiesCount = propertiesCount,
        ViewCount = o.ViewCount
    };
}
