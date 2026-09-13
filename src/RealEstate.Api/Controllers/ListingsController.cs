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
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPhotoUploadService _photoUploadService;

    public ListingsController(ApplicationDbContext db, IPhotoUploadService photoUploadService)
    {
        _db = db;
        _photoUploadService = photoUploadService;
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
            .Select(l => l.ToDto())
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

        return listing is null ? NotFound() : Ok(listing.ToDto());
    }

    // Listings owned by the current user (for the "My Listings" dashboard)
    [Authorize(Roles = "Owner")]
    [HttpGet("mine")]
    public async Task<ActionResult<List<ListingDto>>> Mine()
    {
        var userId = User.GetUserId();
        var listings = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Where(l => l.OwnerId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(l => l.ToDto()));
    }

    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create([FromForm] ListingCreateDto dto)
    {
        // Generated up front so newly uploaded photos can be namespaced under this listing's
        // own id in S3 from the very first upload, rather than a temp/owner-scoped prefix.
        var listingId = Guid.NewGuid();

        var (imageUrls, uploadError) = await BuildImageUrlsAsync(dto.ExistingImageUrls, dto.Photos, $"listings/{listingId}");
        if (uploadError is not null) return uploadError;

        var listing = new Listing
        {
            Id = listingId,
            OwnerId = User.GetUserId(),
            Title = dto.Title,
            Description = dto.Description,
            ListingType = dto.ListingType,
            PropertyType = dto.PropertyType,
            Price = dto.Price,
            Currency = dto.Currency,
            AddressLine = dto.AddressLine,
            City = dto.City,
            State = dto.State,
            ZipCode = dto.ZipCode ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaSqFt = dto.AreaSqFt,
            YearBuilt = dto.YearBuilt,
            Images = imageUrls.Select((url, idx) => new ListingImage
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

        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, created.ToDto());
    }

    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ListingDto>> Update(Guid id, [FromForm] ListingUpdateDto dto)
    {
        var listing = await _db.Listings.Include(l => l.Images).FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != User.GetUserId()) return Forbid();

        // Upload before touching any existing rows, so a failed upload never destroys photos
        // that were already saved.
        var (imageUrls, uploadError) = await BuildImageUrlsAsync(dto.ExistingImageUrls, dto.Photos, $"listings/{id}");
        if (uploadError is not null) return uploadError;

        listing.Title = dto.Title;
        listing.Description = dto.Description;
        listing.ListingType = dto.ListingType;
        listing.PropertyType = dto.PropertyType;
        listing.Status = dto.Status;
        listing.Price = dto.Price;
        listing.Currency = dto.Currency;
        listing.AddressLine = dto.AddressLine;
        listing.City = dto.City;
        listing.State = dto.State;
        listing.ZipCode = dto.ZipCode ?? string.Empty;
        listing.Latitude = dto.Latitude;
        listing.Longitude = dto.Longitude;
        listing.Bedrooms = dto.Bedrooms;
        listing.Bathrooms = dto.Bathrooms;
        listing.AreaSqFt = dto.AreaSqFt;
        listing.YearBuilt = dto.YearBuilt;
        listing.UpdatedAt = DateTime.UtcNow;

        _db.ListingImages.RemoveRange(listing.Images);
        listing.Images.Clear();
        await _db.SaveChangesAsync();

        foreach (var (url, idx) in imageUrls.Select((url, idx) => (url, idx)))
        {
            _db.ListingImages.Add(new ListingImage
            {
                ListingId = listing.Id,
                Url = url,
                IsPrimary = idx == 0,
                SortOrder = idx
            });
        }

        await _db.SaveChangesAsync();
        return Ok(listing.ToDto());
    }

    [Authorize(Roles = "Owner")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != User.GetUserId()) return Forbid();

        // Soft delete keeps the listing (and its inquiries/favorites) for history
        listing.Status = ListingStatus.Removed;
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Combines already-hosted photo URLs (pasted links, or S3 URLs kept from a previous edit)
    // with newly uploaded files, uploading the latter to S3 and returning one ordered list —
    // existing photos first, then new ones in selection order. The first URL is the primary
    // photo. Returns an error result as-is if any file fails validation or the S3 upload fails.
    private async Task<(List<string> Urls, ActionResult? Error)> BuildImageUrlsAsync(
        List<string>? existingImageUrls, List<IFormFile>? photos, string keyPrefix)
    {
        var urls = new List<string>(existingImageUrls ?? new List<string>());
        if (photos is null || photos.Count == 0) return (urls, null);

        var result = await _photoUploadService.UploadManyAsync(photos, keyPrefix);
        if (result.ErrorKind is not null) return (urls, result.ErrorKind.Value.ToActionResult(this));

        urls.AddRange(result.Urls);
        return (urls, null);
    }
}
