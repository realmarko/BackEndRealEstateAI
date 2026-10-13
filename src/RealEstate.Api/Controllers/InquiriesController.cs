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
[Route("api/inquiries")]
public class InquiriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IFactSheetBuilderService _factSheetBuilder;
    private readonly ILogger<InquiriesController> _logger;

    public InquiriesController(
        ApplicationDbContext db,
        IEmailService emailService,
        IFactSheetBuilderService factSheetBuilder,
        ILogger<InquiriesController> logger)
    {
        _db = db;
        _emailService = emailService;
        _factSheetBuilder = factSheetBuilder;
        _logger = logger;
    }

    // Must be signed in to send an inquiry — the sender's account is what lets a listing owner
    // trust who's asking and lets the sender find their own sent messages later.
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(InquiryCreateDto dto)
    {
        // Images/Owner aren't included here — only the qualifying-timeline branch below ever
        // needs them (for the fact-sheet PDF), and that's the minority of inquiries. See
        // SendFactSheetPdfAsync, which loads them itself only when it actually runs.
        var listing = await _db.Listings
            .Include(l => l.Address)
            .FirstOrDefaultAsync(l => l.Id == dto.ListingId);
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
            await SendFactSheetPdfAsync(dto.SenderEmail, dto.SenderName, listing);
        }

        return NoContent();
    }

    // Emails the sender the fact-sheet PDF (IFactSheetBuilderService) for the listing. The lead
    // has just identified themselves via the inquiry, which is why the owner's real email/contact
    // can go on it (see ListingExtensions.ToDto's isAuthenticated redaction, which the builder
    // bypasses on purpose).
    private async Task SendFactSheetPdfAsync(string toEmail, string toName, Listing listing)
    {
        try
        {
            var pdfBytes = await _factSheetBuilder.BuildAsync(listing);

            var subject = $"Ficha técnica: {listing.Title}";
            var body =
                $"Gracias por tu interés en esta propiedad:\n\n" +
                $"{listing.Title}\n{listing.Address!.ToEmailLine()}\n{listing.Currency} {listing.Price}\n\n" +
                "Adjuntamos la ficha técnica en PDF con todas las características. Un agente se pondrá en contacto contigo pronto.";
            var fileName = $"ficha-tecnica-{listing.Id}.pdf";
            await _emailService.SendAsync(toEmail, toName, subject, body, new EmailAttachment(pdfBytes, fileName, "application/pdf"));
        }
        // Deliberately catches everything, not a filtered list: the inquiry above this call
        // already saved successfully, so nothing in here — SMTP, formatting, or an exception
        // thrown by QuestPDF/SkiaSharp while rendering a malformed downloaded photo, which isn't
        // an enumerable/predictable exception type — may ever turn into an error response for a
        // request that already succeeded.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send fact-sheet email to {Email} for listing {ListingId}", toEmail, listing.Id);
        }
    }

    // Inquiries received on listings owned by the current user, plus — for an Admin — every
    // IsOwnerContactNotification row regardless of which listing/owner it's on (see Inquiry.
    // IsOwnerContactNotification's comment): those are the only record of a "contact the owner"
    // message, and the real owner they were emailed to has no account here to see them otherwise.
    [Authorize(Roles = "Owner,Admin")]
    [HttpGet("received")]
    public async Task<ActionResult<List<InquiryDto>>> Received()
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");
        var inquiries = await _db.Inquiries
            .Include(i => i.Listing)
            .Where(i => i.Listing!.OwnerId == userId || (isAdmin && i.IsOwnerContactNotification))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return Ok(inquiries.Select(ToDto));
    }

    [Authorize(Roles = "Owner,Admin")]
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var inquiry = await _db.Inquiries.Include(i => i.Listing).FirstOrDefaultAsync(i => i.Id == id);
        if (inquiry is null) return NotFound();
        var isAdmin = User.IsInRole("Admin");
        if (inquiry.Listing!.OwnerId != User.GetUserId() && !(isAdmin && inquiry.IsOwnerContactNotification))
        {
            return Forbid();
        }

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
        CreatedAt = i.CreatedAt,
        IsOwnerContactNotification = i.IsOwnerContactNotification,
        OwnerContactName = i.IsOwnerContactNotification ? i.Listing?.OwnerContactName : null,
        OwnerContactPhone = i.IsOwnerContactNotification ? i.Listing?.OwnerContactPhone : null,
        OwnerContactEmail = i.IsOwnerContactNotification ? i.Listing?.OwnerContactEmail : null
    };
}
