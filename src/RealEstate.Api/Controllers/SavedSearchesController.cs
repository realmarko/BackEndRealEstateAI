using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/saved-searches")]
[Authorize]
public class SavedSearchesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public SavedSearchesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<SavedSearchDto>>> Mine()
    {
        var userId = User.GetUserId();
        var searches = await _db.SavedSearches
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => ToDto(s))
            .ToListAsync();

        return Ok(searches);
    }

    [HttpPost]
    public async Task<ActionResult<SavedSearchDto>> Create(CreateSavedSearchDto dto)
    {
        // [Required] on Name only rejects null/empty, not whitespace-only — checked explicitly
        // so a blank name can't reach the DB and render as an empty heading on the search's card.
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Name is required." });

        if (dto.MinPrice.HasValue && dto.MaxPrice.HasValue && dto.MinPrice > dto.MaxPrice)
            return BadRequest(new { message = "Min price can't be greater than max price." });

        var userId = User.GetUserId();
        var search = new SavedSearch
        {
            UserId = userId,
            Name = dto.Name.Trim(),
            ListingType = dto.ListingType,
            PropertyType = dto.PropertyType,
            MinPrice = dto.MinPrice,
            MaxPrice = dto.MaxPrice,
            MinBedrooms = dto.MinBedrooms,
            MinBathrooms = dto.MinBathrooms,
            City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim()
        };

        _db.SavedSearches.Add(search);
        await _db.SaveChangesAsync();

        // No single-item GET exists for a saved search (Mine only returns the caller's full
        // list), so CreatedAtAction(nameof(Mine), ...) would point a 201's Location header at
        // the wrong resource — plain 200 with the created object avoids that without inventing
        // a GET-by-id endpoint nothing else needs.
        return Ok(ToDto(search));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.GetUserId();
        var search = await _db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (search is null) return NotFound();

        _db.SavedSearches.Remove(search);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static SavedSearchDto ToDto(SavedSearch s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ListingType = s.ListingType?.ToString(),
        PropertyType = s.PropertyType?.ToString(),
        MinPrice = s.MinPrice,
        MaxPrice = s.MaxPrice,
        MinBedrooms = s.MinBedrooms,
        MinBathrooms = s.MinBathrooms,
        City = s.City,
        CreatedAt = s.CreatedAt
    };
}
