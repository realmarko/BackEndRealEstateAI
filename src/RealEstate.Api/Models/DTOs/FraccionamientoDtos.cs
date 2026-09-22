using System.ComponentModel.DataAnnotations;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

// One raw detection from a single source — n8n sends a batch of these per scraping run. Deliberately
// loose/permissive (nothing beyond Name/City/State/coordinates/SourceType is required): a source
// like a municipal gazette PDF may only yield a name and an approximate location, and the point of
// ingestion is to capture it as a lead for a human to complete, not to reject anything incomplete.
public class FraccionamientoCandidateDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? DeveloperName { get; set; }
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string State { get; set; } = string.Empty;
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [MaxLength(100)] public string? Stage { get; set; }

    [Required] public FraccionamientoSourceType SourceType { get; set; }
    [MaxLength(2048)] public string? SourceUrl { get; set; }
    // Untouched payload from the source (whatever JSON/text n8n scraped) — kept for audit if a
    // match later turns out wrong. Capped generously above what the DB column needs since a
    // scraped HTML snippet can run long, but still bounded so a malformed workflow can't write
    // unbounded rows.
    [MaxLength(20000)] public string? RawData { get; set; }
}

// Body for POST /api/fraccionamientos (admin-only manual add) — same shape as
// FraccionamientoCandidateDto minus SourceType/SourceUrl/RawData, which the controller fills in
// itself (SourceType.ManualAdmin, no URL/raw payload) before handing this to the same
// IFraccionamientoIngestionService.IngestAsync every n8n-sourced candidate goes through, so a
// manual entry gets the exact same dedup-against-existing-records behavior.
public class CreateFraccionamientoDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? DeveloperName { get; set; }
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string State { get; set; } = string.Empty;
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [MaxLength(100)] public string? Stage { get; set; }
}

public class CreateFraccionamientoResultDto
{
    public Guid Id { get; set; }
    // True if this matched an already-existing record (any status) instead of creating a new
    // one — the admin form uses this to tell the admin "this matched an existing candidate"
    // rather than implying a brand-new row was created.
    public bool MatchedExisting { get; set; }
}

public class IngestCandidatesRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public List<FraccionamientoCandidateDto> Candidates { get; set; } = [];
}

public class IngestCandidatesResultDto
{
    // New Fraccionamiento rows created from a candidate that matched nothing existing.
    public int Created { get; set; }
    // Candidates that matched an existing Fraccionamiento (any status) and were recorded as an
    // additional FraccionamientoSource on it instead of creating a duplicate.
    public int MatchedExisting { get; set; }
}

// One row in the admin review queue — deliberately thin (no Description/AmenitiesJson/etc.),
// same reasoning as ErrorLogGroupDto omitting StackTrace: those only matter once an admin expands
// a specific candidate, not while scanning the list.
public class FraccionamientoListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DeveloperName { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int SourceCount { get; set; }
    public DateTime FirstDetectedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class FraccionamientoSourceDto
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public DateTime DetectedAt { get; set; }
}

public class FraccionamientoDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DeveloperName { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Stage { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AmenitiesJson { get; set; }
    public string? MasterPlanImageUrl { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime FirstDetectedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int LinkedListingCount { get; set; }
    public List<FraccionamientoSourceDto> Sources { get; set; } = [];
}

// Body for PATCH /{id}/approve — the admin is expected to have reviewed/corrected everything
// here (a raw candidate's Name/City/State/etc. came straight from a scraper and may be messy),
// so this re-states the full editable surface rather than only the fields an approval logically
// "adds" (Description/Amenities/contact info). MasterPlanImageUrl is a plain URL for now, not a
// file upload — pairing this with the existing S3 photo-upload flow is a reasonable fast-follow,
// not part of this first pass.
public class ApproveFraccionamientoDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? DeveloperName { get; set; }
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string State { get; set; } = string.Empty;
    [MaxLength(100)] public string? Stage { get; set; }
    [MaxLength(5000)] public string? Description { get; set; }
    [MaxLength(4000)] public string? AmenitiesJson { get; set; }
    [MaxLength(2048)] public string? MasterPlanImageUrl { get; set; }
    [MaxLength(30)] public string? ContactPhone { get; set; }
    [EmailAddress, MaxLength(200)] public string? ContactEmail { get; set; }
}

public class MergeFraccionamientoDto
{
    [Required] public Guid TargetFraccionamientoId { get; set; }
}

// Public-facing shapes are deliberately separate types from the admin ones above, not the same
// DTO with fields left blank: Sources/FirstDetectedAt/Status are detection-internal metadata that
// should never leak to a visitor, and keeping a dedicated type means a future field added to the
// admin DTOs doesn't silently become public just because someone forgot to check.
public class FraccionamientoPublicListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DeveloperName { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string? MasterPlanImageUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    // Included here (not just on the detail DTO) so the public map can plot every published
    // development as a marker without an extra round-trip per pin.
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class FraccionamientoPublicDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DeveloperName { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Stage { get; set; }
    public string? Description { get; set; }
    public string? AmenitiesJson { get; set; }
    public string? MasterPlanImageUrl { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<ListingDto> Listings { get; set; } = [];
}

// Body for POST /{id}/contact — mirrors ContactAgentDto exactly (same fields, same limits), since
// this is the same "anonymous visitor emails someone about a property" action, just routed to a
// Fraccionamiento's ContactEmail instead of an Agent's.
public class ContactFraccionamientoDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(5000)] public string Message { get; set; } = string.Empty;
}
