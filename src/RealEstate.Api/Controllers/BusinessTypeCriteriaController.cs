using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Controllers;

// Read-only for anyone (powers the viability-report explanations), Admin-editable at runtime —
// unlike LandUseCategoriesController's fixed catalog, these thresholds are explicitly provisional
// initial estimates (see BusinessTypeCriteria's own doc comment) meant to be tuned as real
// outcomes come in, without a deploy.
[ApiController]
[Route("api/business-type-criteria")]
public class BusinessTypeCriteriaController : ControllerBase
{
    private const string CacheKey = "business-type-criteria";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public BusinessTypeCriteriaController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessTypeCriteriaDto>>> GetAll()
    {
        var criteria = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var rows = await _db.BusinessTypeCriteria.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
            return rows.Select(ToDto).ToList();
        });

        return Ok(criteria);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<BusinessTypeCriteriaDto>> Update(int id, BusinessTypeCriteriaUpdateDto dto)
    {
        var criterion = await _db.BusinessTypeCriteria.FindAsync(id);
        if (criterion is null) return NotFound();

        criterion.MinFrontageMeters = dto.MinFrontageMeters;
        criterion.MinLotSizeSqm = dto.MinLotSizeSqm;
        criterion.PrefersCornerLot = dto.PrefersCornerLot;
        criterion.PrefersPrimaryRoadAccess = dto.PrefersPrimaryRoadAccess;
        criterion.PopulationRadiusKm = dto.PopulationRadiusKm;
        criterion.MinPopulationInRadius = dto.MinPopulationInRadius;
        criterion.CompetitorExclusionRadiusMeters = dto.CompetitorExclusionRadiusMeters;
        criterion.MinCusRatio = dto.MinCusRatio;
        criterion.AcceptableLandUseCategories = dto.AcceptableLandUseCategories;
        criterion.Notes = dto.Notes;

        await _db.SaveChangesAsync();
        _cache.Remove(CacheKey);

        return Ok(ToDto(criterion));
    }

    private static BusinessTypeCriteriaDto ToDto(Models.Entities.BusinessTypeCriteria c) => new()
    {
        Id = c.Id,
        Key = c.Key,
        DisplayName = c.DisplayName,
        MinFrontageMeters = c.MinFrontageMeters,
        MinLotSizeSqm = c.MinLotSizeSqm,
        PrefersCornerLot = c.PrefersCornerLot,
        PrefersPrimaryRoadAccess = c.PrefersPrimaryRoadAccess,
        PopulationRadiusKm = c.PopulationRadiusKm,
        MinPopulationInRadius = c.MinPopulationInRadius,
        CompetitorExclusionRadiusMeters = c.CompetitorExclusionRadiusMeters,
        MinCusRatio = c.MinCusRatio,
        AcceptableLandUseCategories = c.AcceptableLandUseCategories,
        Notes = c.Notes
    };
}
