using System.Net.Mail;
using System.Security.Claims;
using Amazon.S3;
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
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    // Every new agent starts with this seeded 5-star review so their profile isn't blank —
    // not a real customer, always attributed as "Agente Real Estate".
    private const string SystemReviewerName = "Agente Real Estate";

    private readonly RealEstateDbContext _db;
    private readonly IS3UploadService _s3Service;
    private readonly IEmailService _emailService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(RealEstateDbContext db, IS3UploadService s3Service, IEmailService emailService, ILogger<AgentsController> logger)
    {
        _db = db;
        _s3Service = s3Service;
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
            query = query.Where(a => a.Company != null && a.Company.ToLower().Contains(q.Company!.ToLower()));

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
                Company = a.Company,
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
                Company = a.Company,
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

        var agent = new Agent
        {
            UserId = userId,
            Name = $"{User.FindFirstValue("firstName")} {User.FindFirstValue("lastName")}".Trim(),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Phone = dto.Phone,
            Company = company,
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

        // Saved separately from the agent above: a concurrent signup racing to add the same
        // brand-new brokerage name would hit its own unique index, which must not be mistaken
        // for the UserId conflict above and must not roll back the agent that was just created.
        await UpsertBrokerageAsync(company);

        return CreatedAtAction(nameof(GetById), new { id = agent.Id }, ToDto(agent, isOwnProfile: true));
    }

    // Returns the current user's own agent profile — lets the edit form load without knowing its id.
    [Authorize(Roles = "Agent")]
    [HttpGet("me")]
    public async Task<ActionResult<AgentDto>> GetMine()
    {
        var userId = User.GetUserId();
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
        return agent is null ? NotFound() : Ok(ToDto(agent, isOwnProfile: true));
    }

    // Updates the current user's own agent profile. Never takes an id from the client — the row
    // to update is found by UserId from the token, so an agent can only ever edit their own.
    [Authorize(Roles = "Agent")]
    [HttpPut("me")]
    public async Task<ActionResult<AgentDto>> UpdateMine([FromForm] CreateAgentDto dto)
    {
        var userId = User.GetUserId();
        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.UserId == userId);
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
        agent.Company = company;
        agent.IsIndependent = dto.IsIndependent;
        agent.Bio = dto.Bio;
        agent.Specialties = ParseSpecialties(dto.Specialties);

        await _db.SaveChangesAsync();

        // Saved separately, same reasoning as Create: a concurrent request adding the same
        // brand-new brokerage name must not roll back the profile update that just succeeded.
        await UpsertBrokerageAsync(company);

        return Ok(ToDto(agent, isOwnProfile: true));
    }

    // Adds a brokerage name to the catalog if it's new, saved in its own SaveChangesAsync so a
    // concurrent request hitting the unique index on Name can't be mistaken for — or roll back —
    // whatever the caller just saved (an agent create/update). Shared by Create and UpdateMine.
    private async Task UpsertBrokerageAsync(string? company)
    {
        if (company is null || await _db.Brokerages.AnyAsync(b => b.Name.ToLower() == company.ToLower()))
            return;

        _db.Brokerages.Add(new Brokerage { Name = company });
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another concurrent request already inserted this brokerage name — fine either way.
        }
    }

    // Validates and uploads a photo to S3, returning either the resulting URL or an error result
    // ready to return as-is — shared by Create and UpdateMine so the rules can't drift apart.
    private async Task<(string? Url, ActionResult? Error)> TryUploadPhotoAsync(IFormFile photo, string keyPrefix)
    {
        if (!AllowedPhotoTypes.Contains(photo.ContentType))
            return (null, BadRequest(new { message = "Photo must be a JPEG, PNG, or WEBP image." }));
        if (photo.Length > MaxPhotoBytes)
            return (null, BadRequest(new { message = "Photo must be 5 MB or smaller." }));

        try
        {
            var url = await _s3Service.UploadFileAsync(photo, keyPrefix);
            return (url, null);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "Failed to upload agent photo to S3 for prefix {KeyPrefix}", keyPrefix);
            return (null, StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not upload the photo right now. Please try again." }));
        }
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
