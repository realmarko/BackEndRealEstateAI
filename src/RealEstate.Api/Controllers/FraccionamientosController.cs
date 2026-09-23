using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/fraccionamientos")]
public class FraccionamientosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFraccionamientoIngestionService _ingestionService;
    private readonly FraccionamientoOptions _options;
    private readonly IEmailService _emailService;
    private readonly ILogger<FraccionamientosController> _logger;

    public FraccionamientosController(
        ApplicationDbContext db,
        IFraccionamientoIngestionService ingestionService,
        IOptions<FraccionamientoOptions> options,
        IEmailService emailService,
        ILogger<FraccionamientosController> logger)
    {
        _db = db;
        _ingestionService = ingestionService;
        _options = options.Value;
        _emailService = emailService;
        _logger = logger;
    }

    // POST /api/fraccionamientos/candidates — where n8n's scraping workflows push what they find.
    // No human user is behind this call, so it can't use the normal JWT auth — protected instead
    // by a shared secret in the X-Ingestion-Key header, checked in constant time so response
    // timing can't be used to guess the key one byte at a time.
    [HttpPost("candidates")]
    public async Task<ActionResult<IngestCandidatesResultDto>> IngestCandidates(IngestCandidatesRequest dto)
    {
        if (!IsAuthorizedIngestionCaller())
            return Unauthorized(new { message = "Missing or invalid X-Ingestion-Key." });

        var result = await _ingestionService.IngestAsync(dto.Candidates);
        return Ok(result);
    }

    // POST /api/fraccionamientos — an admin adding a development by hand (no n8n workflow wired
    // up for its source yet, or it just doesn't come from an automatable source at all). Goes
    // through the exact same dedup logic as the n8n path via IngestOneAsync, tagged as a
    // FraccionamientoSourceType.ManualAdmin source, and lands in the same Candidate review queue
    // — an admin still has to Approve it before it's public, same as any scraped detection.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreateFraccionamientoResultDto>> Create(CreateFraccionamientoDto dto)
    {
        var candidate = new FraccionamientoCandidateDto
        {
            Name = dto.Name,
            DeveloperName = dto.DeveloperName,
            City = dto.City,
            State = dto.State,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Stage = dto.Stage,
            SourceType = FraccionamientoSourceType.ManualAdmin
        };

        var (fraccionamiento, matchedExisting) = await _ingestionService.IngestOneAsync(candidate);
        return Ok(new CreateFraccionamientoResultDto { Id = fraccionamiento.Id, MatchedExisting = matchedExisting });
    }

    // GET /api/fraccionamientos?status=Candidate&page=1&pageSize=20 — the admin review queue,
    // same paging shape as GET /api/errors. Newest detection first, so a candidate ingested this
    // week doesn't get buried under ones from months ago that nobody's acted on.
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<FraccionamientoListItemDto>>> List(
        [FromQuery] FraccionamientoStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Fraccionamientos.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.FirstDetectedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(f => new FraccionamientoListItemDto
            {
                Id = f.Id,
                Name = f.Name,
                DeveloperName = f.DeveloperName,
                City = f.City,
                State = f.State,
                Status = f.Status.ToString(),
                SourceCount = f.Sources.Count,
                FirstDetectedAt = f.FirstDetectedAt,
                PublishedAt = f.PublishedAt
            })
            .ToListAsync();

        return Ok(new PagedResult<FraccionamientoListItemDto> { Items = items, Page = safePage, PageSize = safePageSize, TotalCount = totalCount });
    }

    // GET /api/fraccionamientos/{id} — full detail an admin sees when expanding a candidate,
    // including every source that's contributed to it so far.
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<FraccionamientoDetailDto>> GetById(Guid id)
    {
        var entity = await _db.Fraccionamientos.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        if (entity is null) return NotFound();

        // Projected instead of .Include(f => f.Sources): a source's RawDataJson can run up to
        // 20,000 chars, and FraccionamientoSourceDto never uses it — loading every source's full
        // raw payload just to discard it on every admin panel expand is pure waste.
        var sources = await _db.FraccionamientoSources
            .Where(s => s.FraccionamientoId == id)
            .OrderByDescending(s => s.DetectedAt)
            .Select(s => new FraccionamientoSourceDto
            {
                Id = s.Id,
                SourceType = s.SourceType.ToString(),
                SourceUrl = s.SourceUrl,
                DetectedAt = s.DetectedAt
            })
            .ToListAsync();

        var linkedListingCount = await _db.Listings.CountAsync(l => l.FraccionamientoId == id);

        return Ok(new FraccionamientoDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DeveloperName = entity.DeveloperName,
            City = entity.City,
            State = entity.State,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            Stage = entity.Stage,
            Status = entity.Status.ToString(),
            Description = entity.Description,
            AmenitiesJson = entity.AmenitiesJson,
            MasterPlanImageUrl = entity.MasterPlanImageUrl,
            ContactPhone = entity.ContactPhone,
            ContactEmail = entity.ContactEmail,
            FirstDetectedAt = entity.FirstDetectedAt,
            PublishedAt = entity.PublishedAt,
            LinkedListingCount = linkedListingCount,
            Sources = sources
        });
    }

    // GET /api/fraccionamientos/published?page=1&pageSize=20 — the public listing grid. No status
    // query param accepted (unlike the admin List above): hardcoding the filter here, rather than
    // trusting a caller-supplied status, means there's no way for this endpoint to ever return a
    // Candidate/UnderReview/Rejected/MergedInto record even by mistake. Newest-published first.
    [HttpGet("published")]
    public async Task<ActionResult<PagedResult<FraccionamientoPublicListItemDto>>> ListPublished(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Fraccionamientos.AsNoTracking().Where(f => f.Status == FraccionamientoStatus.Published);

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.PublishedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(f => new FraccionamientoPublicListItemDto
            {
                Id = f.Id,
                Name = f.Name,
                DeveloperName = f.DeveloperName,
                City = f.City,
                State = f.State,
                Stage = f.Stage,
                MasterPlanImageUrl = f.MasterPlanImageUrl,
                PublishedAt = f.PublishedAt,
                Latitude = f.Latitude,
                Longitude = f.Longitude
            })
            .ToListAsync();

        return Ok(new PagedResult<FraccionamientoPublicListItemDto> { Items = items, Page = safePage, PageSize = safePageSize, TotalCount = totalCount });
    }

    // GET /api/fraccionamientos/published/{id} — public detail page: amenities, master plan, and
    // every Listing ("unit") currently linked to this development, in the same shape the rest of
    // the app already shows listings in.
    [HttpGet("published/{id:guid}")]
    public async Task<ActionResult<FraccionamientoPublicDetailDto>> GetPublishedDetail(Guid id)
    {
        var entity = await _db.Fraccionamientos.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.Status == FraccionamientoStatus.Published);
        if (entity is null) return NotFound();

        var listings = await _db.Listings.AsNoTracking()
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.FraccionamientoId == id && l.Status != ListingStatus.Removed)
            .OrderBy(l => l.Price)
            .ToListAsync();

        return Ok(new FraccionamientoPublicDetailDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DeveloperName = entity.DeveloperName,
            City = entity.City,
            State = entity.State,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            Stage = entity.Stage,
            Description = entity.Description,
            AmenitiesJson = entity.AmenitiesJson,
            MasterPlanImageUrl = entity.MasterPlanImageUrl,
            ContactPhone = entity.ContactPhone,
            ContactEmail = entity.ContactEmail,
            PublishedAt = entity.PublishedAt,
            Listings = listings.Select(l => l.ToDto()).ToList()
        });
    }

    // POST /api/fraccionamientos/published/{id}/contact — sends the visitor's message straight to
    // the developer's email, same pattern as AgentsController.Contact: anonymous and rate-limited
    // instead of authenticated, no DB record kept.
    [EnableRateLimiting("contact")]
    [HttpPost("published/{id:guid}/contact")]
    public async Task<IActionResult> Contact(Guid id, ContactFraccionamientoDto dto)
    {
        var entity = await _db.Fraccionamientos.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.Status == FraccionamientoStatus.Published);
        if (entity is null) return NotFound();

        if (string.IsNullOrWhiteSpace(entity.ContactEmail))
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "This development can't be reached by email right now." });

        var subject = $"New inquiry from {StripControlCharacters(dto.Name)} via RealEstateApp";
        var body =
            $"You have a new message from a potential buyer on RealEstateApp about {entity.Name}.\n\n" +
            $"Name: {dto.Name}\n" +
            $"Phone: {dto.Phone}\n" +
            $"Email: {dto.Email}\n\n" +
            $"Message:\n{dto.Message}";

        try
        {
            await _emailService.SendAsync(entity.ContactEmail, entity.DeveloperName ?? entity.Name, subject, body);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to send contact email for fraccionamiento {FraccionamientoId}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not send the message right now. Please try again." });
        }

        return NoContent();
    }

    // PATCH /api/fraccionamientos/{id}/approve — the admin has filled in/corrected everything in
    // the body (see ApproveFraccionamientoDto), and this is what makes the record visible on the
    // public site.
    [HttpPatch("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Approve(Guid id, ApproveFraccionamientoDto dto)
    {
        var entity = await _db.Fraccionamientos.FindAsync(id);
        if (entity is null) return NotFound();
        if (entity.Status == FraccionamientoStatus.MergedInto)
            return BadRequest(new { message = "This record was merged into another one and can't be published on its own." });

        entity.Name = dto.Name;
        entity.DeveloperName = dto.DeveloperName;
        entity.City = dto.City;
        entity.State = dto.State;
        entity.Stage = dto.Stage;
        entity.Description = dto.Description;
        entity.AmenitiesJson = dto.AmenitiesJson;
        entity.MasterPlanImageUrl = dto.MasterPlanImageUrl;
        entity.ContactPhone = dto.ContactPhone;
        entity.ContactEmail = dto.ContactEmail;
        // Only stamp PublishedAt on the transition into Published — re-approving an already-
        // published record (an admin fixing a typo later) must not bump it to "now" and make a
        // months-old development look newly published on a future "most recent" sort.
        if (entity.Status != FraccionamientoStatus.Published)
            entity.PublishedAt = DateTime.UtcNow;
        entity.Status = FraccionamientoStatus.Published;
        entity.ReviewedByUserId = User.GetUserId();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // PATCH /api/fraccionamientos/{id}/reject — a false positive (a scraper matched something
    // that isn't actually a new development). Left in the table rather than deleted, same
    // reasoning as ErrorLog never deleting rows: keeps the audit trail of what was detected and
    // why it was dismissed, and stops the exact same false positive from being re-created if a
    // source detects it again (the ingestion dedup matches against any status except MergedInto).
    [HttpPatch("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var entity = await _db.Fraccionamientos.FindAsync(id);
        if (entity is null) return NotFound();
        if (entity.Status == FraccionamientoStatus.MergedInto)
            return BadRequest(new { message = "This record was merged into another one and can't be rejected on its own." });

        entity.Status = FraccionamientoStatus.Rejected;
        entity.ReviewedByUserId = User.GetUserId();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // PATCH /api/fraccionamientos/{id}/merge — for a duplicate the automated matching in
    // FraccionamientoIngestionService missed (e.g. the name/location drifted just outside its
    // thresholds). Moves this record's sources onto the target instead of leaving them stranded
    // on a now-merged-away row, so the target ends up with the complete detection history.
    [HttpPatch("{id:guid}/merge")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Merge(Guid id, MergeFraccionamientoDto dto)
    {
        if (id == dto.TargetFraccionamientoId)
            return BadRequest(new { message = "A record can't be merged into itself." });

        var entity = await _db.Fraccionamientos.Include(f => f.Sources).FirstOrDefaultAsync(f => f.Id == id);
        if (entity is null) return NotFound();

        var target = await _db.Fraccionamientos.FindAsync(dto.TargetFraccionamientoId);
        if (target is null) return BadRequest(new { message = "Target fraccionamiento does not exist." });
        // Merging into a record that was itself merged away (or already dismissed) would strand
        // this data behind a dead end instead of the real survivor — the caller should resolve
        // the target's own MergedIntoId first and merge into that record instead.
        if (target.Status == FraccionamientoStatus.MergedInto)
            return BadRequest(new { message = "Target fraccionamiento was itself merged into another record — merge into that one instead." });
        if (target.Status == FraccionamientoStatus.Rejected)
            return BadRequest(new { message = "Target fraccionamiento was rejected and can't receive a merge." });

        foreach (var source in entity.Sources)
            source.FraccionamientoId = dto.TargetFraccionamientoId;

        // Any listings already linked to the merged-away record follow it to the survivor too —
        // otherwise approving the target later wouldn't show units that were correctly attached
        // to what turned out to be the same development under a different record.
        var linkedListings = await _db.Listings.Where(l => l.FraccionamientoId == id).ToListAsync();
        foreach (var listing in linkedListings)
            listing.FraccionamientoId = dto.TargetFraccionamientoId;

        entity.Status = FraccionamientoStatus.MergedInto;
        entity.MergedIntoId = dto.TargetFraccionamientoId;
        entity.ReviewedByUserId = User.GetUserId();

        await _db.SaveChangesAsync();
        return NoContent();
    }

    private bool IsAuthorizedIngestionCaller()
    {
        var provided = Request.Headers["X-Ingestion-Key"].ToString();
        if (string.IsNullOrEmpty(provided)) return false;

        var expectedBytes = Encoding.UTF8.GetBytes(_options.IngestionApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        // Lengths necessarily differ for most wrong guesses, and comparing that first is not
        // itself a timing leak worth avoiding — only the byte-by-byte comparison of same-length
        // input needs to be constant-time.
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string StripControlCharacters(string value) =>
        new(value.Where(c => !char.IsControl(c)).ToArray());
}
