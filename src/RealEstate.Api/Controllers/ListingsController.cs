using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ListingsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/listings?city=Austin&listingType=Sale&minPrice=100000&...
    [HttpGet]
    public async Task<ActionResult<PagedResult<ListingDto>>> Search([FromQuery] ListingSearchQuery q)
    {
        var query = _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Where(l => l.Status != ListingStatus.Removed)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.City))
            query = query.Where(l => l.City.ToLower() == q.City!.ToLower());
        if (q.ListingType.HasValue)
            query = query.Where(l => l.ListingType == q.ListingType);
        if (q.PropertyType.HasValue)
            query = query.Where(l => l.PropertyType == q.PropertyType);
        if (q.MinPrice.HasValue)
            query = query.Where(l => l.Price >= q.MinPrice);
        if (q.MaxPrice.HasValue)
            query = query.Where(l => l.Price <= q.MaxPrice);
        if (q.MinBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= q.MinBedrooms);
        if (q.MinBathrooms.HasValue)
            query = query.Where(l => l.Bathrooms >= q.MinBathrooms);

        // Google Maps viewport bounding box filter (pan/zoom search)
        if (q.SwLat.HasValue && q.SwLng.HasValue && q.NeLat.HasValue && q.NeLng.HasValue)
        {
            query = query.Where(l =>
                l.Latitude >= q.SwLat && l.Latitude <= q.NeLat &&
                l.Longitude >= q.SwLng && l.Longitude <= q.NeLng);
        }

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => ToDto(l))
            .ToListAsync();

        return Ok(new PagedResult<ListingDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListingDto>> GetById(Guid id)
    {
        var listing = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .FirstOrDefaultAsync(l => l.Id == id);

        return listing is null ? NotFound() : Ok(ToDto(listing));
    }

    // Listings owned by the current user (for the "My Listings" dashboard)
    [Authorize(Roles = "Owner")]
    [HttpGet("mine")]
    public async Task<ActionResult<List<ListingDto>>> Mine()
    {
        var userId = CurrentUserId();
        var listings = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Where(l => l.OwnerId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(ToDto));
    }

    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create(ListingCreateDto dto)
    {
        var listing = new Listing
        {
            OwnerId = CurrentUserId(),
            Title = dto.Title,
            Description = dto.Description,
            ListingType = dto.ListingType,
            PropertyType = dto.PropertyType,
            Price = dto.Price,
            AddressLine = dto.AddressLine,
            City = dto.City,
            State = dto.State,
            ZipCode = dto.ZipCode,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaSqFt = dto.AreaSqFt,
            YearBuilt = dto.YearBuilt,
            Images = dto.ImageUrls.Select((url, idx) => new ListingImage
            {
                Url = url,
                IsPrimary = idx == 0,
                SortOrder = idx
            }).ToList()
        };

        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        var created = await _db.Listings
            .Include(l => l.Images).Include(l => l.Owner)
            .FirstAsync(l => l.Id == listing.Id);

        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, ToDto(created));
    }

    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ListingDto>> Update(Guid id, ListingUpdateDto dto)
    {
        var listing = await _db.Listings.Include(l => l.Images).FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != CurrentUserId()) return Forbid();

        listing.Title = dto.Title;
        listing.Description = dto.Description;
        listing.ListingType = dto.ListingType;
        listing.PropertyType = dto.PropertyType;
        listing.Status = dto.Status;
        listing.Price = dto.Price;
        listing.AddressLine = dto.AddressLine;
        listing.City = dto.City;
        listing.State = dto.State;
        listing.ZipCode = dto.ZipCode;
        listing.Latitude = dto.Latitude;
        listing.Longitude = dto.Longitude;
        listing.Bedrooms = dto.Bedrooms;
        listing.Bathrooms = dto.Bathrooms;
        listing.AreaSqFt = dto.AreaSqFt;
        listing.YearBuilt = dto.YearBuilt;
        listing.UpdatedAt = DateTime.UtcNow;

        _db.ListingImages.RemoveRange(listing.Images);
        listing.Images = dto.ImageUrls.Select((url, idx) => new ListingImage
        {
            ListingId = listing.Id,
            Url = url,
            IsPrimary = idx == 0,
            SortOrder = idx
        }).ToList();

        await _db.SaveChangesAsync();
        return Ok(ToDto(listing));
    }

    [Authorize(Roles = "Owner")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != CurrentUserId()) return Forbid();

        // Soft delete keeps the listing (and its inquiries/favorites) for history
        listing.Status = ListingStatus.Removed;
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private static ListingDto ToDto(Listing l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        ListingType = l.ListingType.ToString(),
        PropertyType = l.PropertyType.ToString(),
        Status = l.Status.ToString(),
        Price = l.Price,
        AddressLine = l.AddressLine,
        City = l.City,
        State = l.State,
        ZipCode = l.ZipCode,
        Latitude = l.Latitude,
        Longitude = l.Longitude,
        Bedrooms = l.Bedrooms,
        Bathrooms = l.Bathrooms,
        AreaSqFt = l.AreaSqFt,
        YearBuilt = l.YearBuilt,
        OwnerId = l.OwnerId,
        OwnerName = l.Owner is null ? string.Empty : $"{l.Owner.FirstName} {l.Owner.LastName}",
        CreatedAt = l.CreatedAt,
        ImageUrls = l.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList()
    };
}
