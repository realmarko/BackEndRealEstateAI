using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/inquiries")]
public class InquiriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public InquiriesController(ApplicationDbContext db) => _db = db;

    // Must be signed in to send an inquiry — the sender's account is what lets a listing owner
    // trust who's asking and lets the sender find their own sent messages later.
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(InquiryCreateDto dto)
    {
        var listingExists = await _db.Listings.AnyAsync(l => l.Id == dto.ListingId);
        if (!listingExists) return NotFound(new { message = "Listing not found" });

        _db.Inquiries.Add(new Inquiry
        {
            ListingId = dto.ListingId,
            SenderId = User.GetUserId(),
            SenderName = dto.SenderName,
            SenderEmail = dto.SenderEmail,
            SenderPhone = dto.SenderPhone,
            Message = dto.Message,
            FundingMethod = dto.FundingMethod,
            Timeline = dto.Timeline,
            HasAgent = dto.HasAgent
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Inquiries received on listings owned by the current user
    [Authorize(Roles = "Owner")]
    [HttpGet("received")]
    public async Task<ActionResult<List<InquiryDto>>> Received()
    {
        var userId = User.GetUserId();
        var inquiries = await _db.Inquiries
            .Include(i => i.Listing)
            .Where(i => i.Listing!.OwnerId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return Ok(inquiries.Select(ToDto));
    }

    [Authorize(Roles = "Owner")]
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var inquiry = await _db.Inquiries.Include(i => i.Listing).FirstOrDefaultAsync(i => i.Id == id);
        if (inquiry is null) return NotFound();
        if (inquiry.Listing!.OwnerId != User.GetUserId()) return Forbid();

        inquiry.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static InquiryDto ToDto(Inquiry i) => new()
    {
        Id = i.Id,
        ListingId = i.ListingId,
        ListingTitle = i.Listing?.Title ?? string.Empty,
        SenderName = i.SenderName,
        SenderEmail = i.SenderEmail,
        SenderPhone = i.SenderPhone,
        Message = i.Message,
        FundingMethod = i.FundingMethod?.ToString(),
        Timeline = i.Timeline?.ToString(),
        HasAgent = i.HasAgent,
        IsRead = i.IsRead,
        CreatedAt = i.CreatedAt
    };
}
