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
    private readonly IFactSheetPdfService _factSheetPdfService;
    private readonly IOwnerAgentLookupService _ownerAgentLookup;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InquiriesController> _logger;

    public InquiriesController(
        ApplicationDbContext db,
        IEmailService emailService,
        IFactSheetPdfService factSheetPdfService,
        IOwnerAgentLookupService ownerAgentLookup,
        IHttpClientFactory httpClientFactory,
        ILogger<InquiriesController> logger)
    {
        _db = db;
        _emailService = emailService;
        _factSheetPdfService = factSheetPdfService;
        _ownerAgentLookup = ownerAgentLookup;
        _httpClientFactory = httpClientFactory;
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

    // Emails the sender a generated PDF fact sheet (specs, address, contact) for the listing —
    // built from the same ListingDto shape the listing-detail page renders from, so it can never
    // show different numbers than what the lead already saw online. The lead has just identified
    // themselves via the inquiry, which is why the owner's real email/contact can go on it (see
    // ListingExtensions.ToDto's isAuthenticated redaction, which this bypasses on purpose).
    private async Task SendFactSheetPdfAsync(string toEmail, string toName, Listing listing)
    {
        try
        {
            // Create()'s query only loads Address; these two are sequential (not WhenAll) because
            // both run against the same ApplicationDbContext, which — like any EF Core context —
            // isn't safe for concurrent operations.
            await _db.Entry(listing).Collection(l => l.Images).LoadAsync();
            await _db.Entry(listing).Reference(l => l.Owner).LoadAsync();

            var dto = listing.ToDto(isAuthenticated: true);

            // Independent of each other (separate DbContext / HttpClient underneath), so run
            // concurrently instead of one after the other — this is the slowest part of handling
            // a qualifying inquiry, so overlapping them matters.
            var photosTask = DownloadPhotosAsync(dto.ImageUrls);
            var agentInfoTask = _ownerAgentLookup.GetAsync(listing.OwnerId);
            await Task.WhenAll(photosTask, agentInfoTask);

            var (company, photoUrl) = agentInfoTask.Result;
            dto.OwnerCompany = company;
            dto.OwnerPhotoUrl = photoUrl;

            var pdfBytes = _factSheetPdfService.Generate(dto, photosTask.Result);

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

    // Downloads up to FactSheetPdfService.MaxPhotos listing photos (S3 URLs, already in
    // ImageUrls' SortOrder) concurrently for the fact sheet's photo strip. Each download is
    // independently best-effort: one broken/slow image (a transient S3 hiccup, a malformed URL,
    // a photo deleted after the listing loaded) is logged and skipped rather than failing the
    // others or the email.
    private async Task<List<byte[]>> DownloadPhotosAsync(List<string> imageUrls)
    {
        var client = _httpClientFactory.CreateClient("FactSheetPhotos");
        var downloads = imageUrls.Take(FactSheetPdfService.MaxPhotos).Select(url => DownloadPhotoAsync(client, url));
        var results = await Task.WhenAll(downloads);
        return results.Where(bytes => bytes is not null).Select(bytes => bytes!).ToList();
    }

    private async Task<byte[]?> DownloadPhotoAsync(HttpClient client, string url)
    {
        try
        {
            return await client.GetByteArrayAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download listing photo {Url} for fact sheet", url);
            return null;
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
