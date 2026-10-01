using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Controllers;

// Read-only catalog powering the listing form's "Uso de suelo" dropdown (shown only for Land
// listings — see listing-form.component.ts). Fixed, admin-seeded list (see LandUseCategory's
// HasData seed in ApplicationDbContext), so there's no create/update/delete here.
[ApiController]
[Route("api/land-use-categories")]
public class LandUseCategoriesController : ControllerBase
{
    private const string CacheKey = "land-use-categories";
    // No admin UI ever changes this catalog at runtime (only a migration seed does), so a long
    // TTL just bounds a cold-start/stale-data worst case rather than tracking real volatility —
    // a deploy (which restarts the process) picks up a seed change immediately regardless.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public LandUseCategoriesController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<List<LandUseCategoryDto>>> GetAll()
    {
        var categories = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await _db.LandUseCategories
                .OrderBy(c => c.Id)
                .Select(c => new LandUseCategoryDto { Id = c.Id, Name = c.Name })
                .ToListAsync();
        });

        return Ok(categories);
    }
}
