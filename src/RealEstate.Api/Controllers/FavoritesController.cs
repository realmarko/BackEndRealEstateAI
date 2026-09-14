using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public FavoritesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<FavoriteDto>>> Mine()
    {
        var userId = User.GetUserId();
        var favorites = await _db.Favorites
            .Include(f => f.Listing).ThenInclude(l => l!.Images)
            .Include(f => f.Listing).ThenInclude(l => l!.Owner)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        // Full ToDto() (same extension every other listing-returning endpoint uses) instead of
        // a hand-rolled partial projection — the frontend's favorites page renders listing
        // cards through the exact same Listing model/adapter as every other page, so it needs
        // the same fields (address, year built, etc.), not a trimmed-down subset.
        return Ok(favorites.Select(f => new FavoriteDto
        {
            Id = f.Id,
            CreatedAt = f.CreatedAt,
            Listing = f.Listing!.ToDto()
        }));
    }

    [HttpPost("{listingId:guid}")]
    public async Task<IActionResult> Add(Guid listingId)
    {
        var userId = User.GetUserId();
        var exists = await _db.Favorites.AnyAsync(f => f.UserId == userId && f.ListingId == listingId);
        if (exists) return NoContent();

        var listingExists = await _db.Listings.AnyAsync(l => l.Id == listingId);
        if (!listingExists) return NotFound();

        _db.Favorites.Add(new Favorite { UserId = userId, ListingId = listingId });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{listingId:guid}")]
    public async Task<IActionResult> Remove(Guid listingId)
    {
        var userId = User.GetUserId();
        var favorite = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.ListingId == listingId);
        if (favorite is null) return NotFound();

        _db.Favorites.Remove(favorite);
        await _db.SaveChangesAsync();
        return NoContent();
    }

}
