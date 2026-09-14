using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    // Every new agent starts with this seeded 5-star review so their profile isn't blank —
    // not a real customer, always attributed as "Agente Real Estate".
    private const string SystemReviewerName = "Agente Real Estate";

    private readonly RealEstateDbContext _db;
    // Agent lives in RealEstateDbContext; Listing lives in ApplicationDbContext (it's really
    // just the Identity DbContext, reused for listings) — there's no EF-enforced FK between
    // them, so linking an agent to their listings takes two round trips, not one join.
    private readonly ApplicationDbContext _listingsDb;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(
        RealEstateDbContext db,
        ApplicationDbContext listingsDb,
        IPhotoUploadService photoUploadService,
        IEmailService emailService,
        ILogger<AgentsController> logger)
    {
        _db = db;
        _listingsDb = listingsDb;
        _photoUploadService = photoUploadService;
        _emailService = emailService;
        _logger = logger;
    }

    // GET /api/agents?name=smith
    [HttpGet]
    public async Task<ActionResult<PagedResult<AgentDto>>> Search([FromQuery] AgentSearchQuery q)
    {
        var currentUserId = User.TryGetUserId();
        var query = _db.Agents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Name))
            query = query.Where(a => a.Name.ToLower().Contains(q.Name!.ToLower()));

        if (!string.IsNullOrWhiteSpace(q.Specialty))
            query = query.Where(a => a.Specialties.Any(s => s.ToLower().Contains(q.Specialty!.ToLower())));

        if (!string.IsNullOrWhiteSpace(q.Company))
            query = query.Where(a => a.Brokerage != null && a.Brokerage.Name.ToLower().Contains(q.Company!.ToLower()));

        if (q.MinRating.HasValue)
            query = query.Where(a => a.Reviews.Any() && a.Reviews.Average(r => (double)r.Rating) >= q.MinRating.Value);

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        // Projects PropertiesCount directly (a SQL COUNT subquery) instead of Include()-ing
        // every Property row just to read .Count in memory.
        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AgentDto
            {
                Id = a.Id,
                IsOwnProfile = currentUserId != null && a.UserId == currentUserId,
                Name = a.Name,
                Email = a.Email,
                Phone = a.Phone,
                Company = a.Brokerage != null ? a.Brokerage.Name : null,
                IsIndependent = a.IsIndependent,
                PhotoUrl = a.PhotoUrl,
                Bio = a.Bio,
                Specialties = a.Specialties,
                PropertiesCount = a.Properties.Count,
                AverageRating = a.Reviews.Any() ? a.Reviews.Average(r => (double)r.Rating) : null,
                ReviewsCount = a.Reviews.Count
            })
            .ToListAsync();

        return Ok(new PagedResult<AgentDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDto>> GetById(int id)
    {
        var currentUserId = User.TryGetUserId();
        var agent = await _db.Agents
            .Where(a => a.Id == id)
            .Select(a => new AgentDto
            {
                Id = a.Id,
                IsOwnProfile = currentUserId != null && a.UserId == currentUserId,
                Name = a.Name,
                Email = a.Email,
                Phone = a.Phone,
                Company = a.Brokerage != null ? a.Brokerage.Name : null,
                IsIndependent = a.IsIndependent,
                PhotoUrl = a.PhotoUrl,
                Bio = a.Bio,
                Specialties = a.Specialties,
                PropertiesCount = a.Properties.Count,
                AverageRating = a.Reviews.Any() ? a.Reviews.Average(r => (double)r.Rating) : null,
                ReviewsCount = a.Reviews.Count
            })
            .FirstOrDefaultAsync();

        return agent is null ? NotFound() : Ok(agent);
    }

    // GET /api/agents/5/listings — the agent's own listings (Listing.OwnerId == Agent.UserId),
    // shown on their public profile so a visitor can see the real properties, not just a count.
    [HttpGet("{id:int}/listings")]
    public async Task<ActionResult<List<ListingDto>>> GetListings(int id)
    {
        var userId = await _db.Agents.Where(a => a.Id == id).Select(a => a.UserId).FirstOrDefaultAsync();
        if (userId is null) return Ok(new List<ListingDto>());

        var listings = await _listingsDb.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Where(l => l.OwnerId == userId && l.Status != ListingStatus.Removed)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(l => l.ToDto()));
    }

    // Completes the agent profile for the currently logged-in user (registered with the Agent role)
    [Authorize(Roles = "Agent")]
    [HttpPost]
    public async Task<ActionResult<AgentDto>> Create([FromForm] CreateAgentDto dto)
    {
        var userId = User.GetUserId();
        if (await _db.Agents.AnyAsync(a => a.UserId == userId))
            return Conflict(new { message = "An agent profile already exists for this account." });

        string? photoUrl = null;
        if (dto.Photo is not null)
        {
            var (uploadedUrl, uploadError) = await TryUploadPhotoAsync(dto.Photo, $"agents/{userId}");
            if (uploadError is not null) return uploadError;
            photoUrl = uploadedUrl;
        }

        // An independent agent has no brokerage, regardless of what was typed before checking the box.
        var company = dto.IsIndependent || string.IsNullOrWhiteSpace(dto.Company) ? null : dto.Company.Trim();
        // Resolved/created before the agent so the FK has a real row to point at — a concurrent
        // signup racing to add the same brand-new brokerage name hits its own unique index,
        // handled inside the helper, and never rolls back the agent below.
        var brokerage = await ResolveOrCreateBrokerageAsync(company);

        var agent = new Agent
        {
            UserId = userId,
            Name = $"{User.FindFirstValue("firstName")} {User.FindFirstValue("lastName")}".Trim(),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Phone = dto.Phone,
            Brokerage = brokerage,
            IsIndependent = dto.IsIndependent,
            PhotoUrl = photoUrl,
            Bio = dto.Bio,
            Specialties = ParseSpecialties(dto.Specialties)
        };

        agent.Reviews.Add(new AgentReview
        {
            ReviewerUserId = Guid.Empty,
            ReviewerName = SystemReviewerName,
            Rating = 5
        });

        _db.Agents.Add(agent);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate submit raced past the AnyAsync check above and hit the
            // unique index on UserId — treat it the same as the check finding it first.
            return Conflict(new { message = "An agent profile already exists for this account." });
        }

        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, ToDto(agent, isOwnProfile: true));
    }

    // Returns the current user's own agent profile — lets the edit form load without knowing its id.
    [Authorize(Roles = "Agent")]
    [HttpGet("me")]
    public async Task<ActionResult<AgentDto>> GetMine()
    {
        var userId = User.GetUserId();
        // ToDto reads a.Reviews/a.Properties/a.Company (via a.Brokerage) in memory, so they
        // must be eager-loaded here — unlike Search/GetById, which project straight to SQL.
        var agent = await _db.Agents
            .Include(a => a.Reviews)
            .Include(a => a.Properties)
            .Include(a => a.Brokerage)
            .FirstOrDefaultAsync(a => a.UserId == userId);
        return agent is null ? NotFound() : Ok(ToDto(agent, isOwnProfile: true));
    }

    // Updates the current user's own agent profile. Never takes an id from the client — the row
    // to update is found by UserId from the token, so an agent can only ever edit their own.
    [Authorize(Roles = "Agent")]
    [HttpPut("me")]
    public async Task<ActionResult<AgentDto>> UpdateMine([FromForm] CreateAgentDto dto)
    {
        var userId = User.GetUserId();
        // Same reasoning as GetMine: ToDto's counts need Reviews/Properties eager-loaded, and
        // Brokerage must be loaded too — assigning agent.Brokerage below only updates the FK
        // correctly if EF has a prior snapshot of that navigation to compare against; on a
        // not-yet-loaded reference navigation, EF's change tracker can't tell "changed to null"
        // from "was already null", and BrokerageId would silently keep its old database value.
        var agent = await _db.Agents
            .Include(a => a.Reviews)
            .Include(a => a.Properties)
            .Include(a => a.Brokerage)
            .FirstOrDefaultAsync(a => a.UserId == userId);
        if (agent is null) return NotFound();

        if (dto.Photo is not null)
        {
            var (uploadedUrl, uploadError) = await TryUploadPhotoAsync(dto.Photo, $"agents/{userId}");
            if (uploadError is not null) return uploadError;
            agent.PhotoUrl = uploadedUrl;
        }

        // An independent agent has no brokerage, regardless of what was typed before checking the box.
        var company = dto.IsIndependent || string.IsNullOrWhiteSpace(dto.Company) ? null : dto.Company.Trim();

        agent.Phone = dto.Phone;
        agent.IsIndependent = dto.IsIndependent;
        agent.Bio = dto.Bio;
        agent.Specialties = ParseSpecialties(dto.Specialties);

        // Skip the lookup/insert entirely when the company text didn't actually change —
        // otherwise every profile save (even one only touching Bio or Phone) pays for a
        // Brokerages round trip. Case-insensitive, matching ResolveOrCreateBrokerageAsync itself.
        if (!string.Equals(company, agent.Brokerage?.Name, StringComparison.OrdinalIgnoreCase))
        {
            // Resolved last, and only the navigation is set: Brokerage is loaded above, so EF's
            // normal change-tracking correctly derives the BrokerageId update from it. Resolving
            // after the scalar fields above means a brand-new-brokerage insert's own SaveChanges
            // (inside the helper) persists those fields as a side effect before the FK is set, so
            // a crash between the two only ever leaves Brokerage stale — never Phone/Bio/etc.
            agent.Brokerage = await ResolveOrCreateBrokerageAsync(company);
        }

        await _db.SaveChangesAsync();

        return Ok(ToDto(agent, isOwnProfile: true));
    }

    // Resolves an existing brokerage by name (case-insensitive, so "Century 21" and "century 21"
    // always land on the same row instead of silently forking the catalog) or creates one.
    // Saved in its own SaveChangesAsync, before the caller's own agent create/update, so the FK
    // has a row to point at; a concurrent request racing to insert the same brand-new name hits
    // its own unique index here, which must not be mistaken for — or roll back — anything the
    // caller does afterwards. Shared by Create and UpdateMine.
    private async Task<Brokerage?> ResolveOrCreateBrokerageAsync(string? company)
    {
        if (company is null) return null;

        var existing = await _db.Brokerages.FirstOrDefaultAsync(b => b.Name.ToLower() == company.ToLower());
        if (existing is not null) return existing;

        var brokerage = new Brokerage { Name = company };
        _db.Brokerages.Add(brokerage);
        try
        {
            await _db.SaveChangesAsync();
            return brokerage;
        }
        catch (DbUpdateException)
        {
            // Only a concurrent insert of this same name explains a failure here (the unique
            // index on Brokerage.Name is the only constraint this insert can violate) — but
            // don't just assume it: if no matching row actually exists, this wasn't that race
            // (a transient DB error, say), so surface the real exception instead of masking it
            // behind a confusing "sequence contains no elements" from a blind FirstAsync.
            var wonByConcurrentRequest = await _db.Brokerages.FirstOrDefaultAsync(b => b.Name.ToLower() == company.ToLower());
            if (wonByConcurrentRequest is not null) return wonByConcurrentRequest;
            throw;
        }
    }

    // Validates and uploads a photo to S3, returning either the resulting URL or an error result
    // ready to return as-is — shared by Create and UpdateMine so the rules can't drift apart.
    private async Task<(string? Url, ActionResult? Error)> TryUploadPhotoAsync(IFormFile photo, string keyPrefix)
    {
        var result = await _photoUploadService.UploadAsync(photo, keyPrefix);
        if (result.ErrorKind is not null) return (null, result.ErrorKind.Value.ToActionResult(this));

        return (result.Urls.FirstOrDefault(), null);
    }

    // GET /api/agents/{id}/reviews
    [HttpGet("{id:int}/reviews")]
    public async Task<ActionResult<List<AgentReviewDto>>> GetReviews(int id)
    {
        var agentExists = await _db.Agents.AnyAsync(a => a.Id == id);
        if (!agentExists) return NotFound();

        var reviews = await _db.AgentReviews
            .Where(r => r.AgentId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AgentReviewDto
            {
                Id = r.Id,
                ReviewerName = r.ReviewerName,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(reviews);
    }

    // Any authenticated user can review an agent, once, as long as it isn't their own profile.
    [Authorize]
    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<AgentReviewDto>> AddReview(int id, CreateAgentReviewDto dto)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();

        var userId = User.GetUserId();
        if (agent.UserId == userId)
            return BadRequest(new { message = "You can't review your own agent profile." });

        if (await _db.AgentReviews.AnyAsync(r => r.AgentId == id && r.ReviewerUserId == userId))
            return Conflict(new { message = "You already reviewed this agent." });

        var review = new AgentReview
        {
            AgentId = id,
            ReviewerUserId = userId,
            ReviewerName = $"{User.FindFirstValue("firstName")} {User.FindFirstValue("lastName")}".Trim(),
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        _db.AgentReviews.Add(review);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Concurrent duplicate submit raced past the AnyAsync check above and hit the
            // unique index on (AgentId, ReviewerUserId) — treat it the same as finding it first.
            return Conflict(new { message = "You already reviewed this agent." });
        }

        // Best-effort notification — the review is already saved, so an email failure here must
        // never turn into an error response for a review that succeeded.
        if (!string.IsNullOrWhiteSpace(agent.Email))
        {
            try
            {
                var subject = $"New review from {StripControlCharacters(review.ReviewerName)} on RealEstateApp";
                var body =
                    $"{review.ReviewerName} left you a {review.Rating}-star review on RealEstateApp.\n\n" +
                    (review.Comment is not null ? $"\"{review.Comment}\"\n\n" : "") +
                    "Log in to your profile to see it.";
                await _emailService.SendAsync(agent.Email, agent.Name, subject, body);
            }
            catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException)
            {
                // Same severity as Contact's failure log below — a silently-broken agent email
                // (e.g. a misconfigured SMTP host) should be just as visible here, since without
                // it the agent never finds out they were reviewed, indefinitely.
                _logger.LogError(ex, "Failed to send review-notification email to agent {AgentId}", id);
            }
        }

        return CreatedAtAction(nameof(GetReviews), new { id }, new AgentReviewDto
        {
            Id = review.Id,
            ReviewerName = review.ReviewerName,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        });
    }

    // Only the agent being reviewed can moderate their own reviews — not the reviewer, not anyone else.
    [Authorize]
    [HttpDelete("{id:int}/reviews/{reviewId:int}")]
    public async Task<IActionResult> DeleteReview(int id, int reviewId)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();

        if (agent.UserId != User.GetUserId())
            return Forbid();

        var review = await _db.AgentReviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.AgentId == id);
        if (review is null) return NotFound();

        // An agent can hide a legitimate negative review this way, so at minimum this needs to be
        // reconstructible after the fact — logged rather than silently vanishing without a trace.
        _logger.LogInformation(
            "Agent {AgentId} deleted review {ReviewId} (rating {Rating}, from reviewer {ReviewerUserId})",
            id, reviewId, review.Rating, review.ReviewerUserId);

        _db.AgentReviews.Remove(review);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // Sends the visitor's message straight to the agent's email — no DB record is kept.
    // Deliberately anonymous (like InquiriesController.Create), so it's rate-limited instead.
    [EnableRateLimiting("contact")]
    [HttpPost("{id:int}/contact")]
    public async Task<IActionResult> Contact(int id, ContactAgentDto dto)
    {
        var agent = await _db.Agents.FindAsync(id);
        if (agent is null) return NotFound();

        if (string.IsNullOrWhiteSpace(agent.Email))
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "This agent can't be reached by email right now." });

        // Only the Subject line is at risk from embedded newlines (MailMessage rejects control
        // characters there) — the body is free text, so dto.Message keeps its own line breaks.
        var subject = $"New inquiry from {StripControlCharacters(dto.Name)} via RealEstateApp";
        var body =
            $"You have a new message from a potential client on RealEstateApp.\n\n" +
            $"Name: {dto.Name}\n" +
            $"Phone: {dto.Phone}\n" +
            $"Email: {dto.Email}\n\n" +
            $"Message:\n{dto.Message}";

        try
        {
            await _emailService.SendAsync(agent.Email, agent.Name, subject, body);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to send contact email to agent {AgentId}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not send the message right now. Please try again." });
        }

        return NoContent();
    }

    // Removes CR/LF (and other control characters) so a Subject built from user input can never
    // hit MailMessage's "not in the form required for a subject" ArgumentException.
    private static string StripControlCharacters(string value) =>
        new(value.Where(c => !char.IsControl(c)).ToArray());

    private static List<string> ParseSpecialties(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static AgentDto ToDto(Agent a, bool isOwnProfile = false) => new()
    {
        Id = a.Id,
        IsOwnProfile = isOwnProfile,
        Name = a.Name,
        Email = a.Email,
        Phone = a.Phone,
        Company = a.Company,
        IsIndependent = a.IsIndependent,
        PhotoUrl = a.PhotoUrl,
        Bio = a.Bio,
        Specialties = a.Specialties,
        PropertiesCount = a.Properties.Count,
        AverageRating = a.Reviews.Count > 0 ? a.Reviews.Average(r => (double)r.Rating) : null,
        ReviewsCount = a.Reviews.Count
    };
}
