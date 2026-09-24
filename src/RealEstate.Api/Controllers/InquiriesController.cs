using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/inquiries")]
public class InquiriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<InquiriesController> _logger;

    public InquiriesController(
        ApplicationDbContext db,
        IEmailService emailService,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<InquiriesController> logger)
    {
        _db = db;
        _emailService = emailService;
        _frontendBaseUrl = frontendOptions.Value.BaseUrl;
        _logger = logger;
    }

    // Must be signed in to send an inquiry — the sender's account is what lets a listing owner
    // trust who's asking and lets the sender find their own sent messages later.
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(InquiryCreateDto dto)
    {
        var listing = await _db.Listings.Include(l => l.Address).FirstOrDefaultAsync(l => l.Id == dto.ListingId);
        if (listing is null) return NotFound(new { message = "Listing not found" });

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

        // Best-effort, same reasoning as NotifySavedSearchesAsync: the inquiry is already
        // saved, so a failure here must never turn into an error response for it. Only sent to
        // leads who actually answered the qualifying questions with real intent — a Timeline of
        // null (the quick-message box never asks) or JustBrowsing doesn't count as "passing".
        if (dto.Timeline is PurchaseTimeline.ReadyNow or PurchaseTimeline.OneToThreeMonths or PurchaseTimeline.ThreeToSixMonths)
        {
            await SendFactSheetLinkAsync(dto.SenderEmail, dto.SenderName, listing);
        }

        return NoContent();
    }

    // Emails the sender a link back to the listing's own detail page (photos, full spec sheet,
    // mortgage calculator, etc. all already live there) rather than generating a separate
    // document — the page is always up to date and this needs no new dependency.
    private async Task SendFactSheetLinkAsync(string toEmail, string toName, Listing listing)
    {
        try
        {
            var listingUrl = $"{_frontendBaseUrl.TrimEnd('/')}/listings/{listing.Id}";
            var subject = $"Ficha técnica: {listing.Title}";
            var body =
                $"Gracias por tu interés en esta propiedad:\n\n" +
                $"{listing.Title}\n{listing.Address!.ToEmailLine()}\n{listing.Currency} {listing.Price}\n\n" +
                $"Consulta la ficha técnica completa (fotos, características y más) aquí:\n{listingUrl}\n\n" +
                "Un agente se pondrá en contacto contigo pronto.";
            await _emailService.SendAsync(toEmail, toName, subject, body);
        }
        // NullReferenceException included alongside the SMTP/formatting failures this filter was
        // written for: the inquiry above this call already saved successfully, so a null
        // listing.Address (which should never happen, but this is a best-effort notification,
        // not the inquiry itself) must not turn into a 500 for a request that already succeeded.
        catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException or NullReferenceException)
        {
            _logger.LogError(ex, "Failed to send fact-sheet email to {Email} for listing {ListingId}", toEmail, listing.Id);
        }
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
