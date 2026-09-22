namespace RealEstate.Api.Models.Entities;

public enum FraccionamientoStatus
{
    // Auto-created by the ingestion endpoint from a single detected source — nobody's looked at
    // it yet.
    Candidate = 0,
    // An admin has opened it and is actively filling in the published-facing details.
    UnderReview = 1,
    Published = 2,
    // A false positive from a source (e.g. a mislabeled listing, not a real new development).
    Rejected = 3,
    // Deduplication missed this one and an admin manually folded it into another record —
    // MergedIntoId points at the survivor.
    MergedInto = 4
}

// A housing development (subdivision) detected from one or more external sources — see
// FraccionamientoSource. Starts as an unreviewed Candidate from the ingestion endpoint and only
// becomes visible to the public once an admin moves it to Published. Individual lots/houses
// inside it are plain Listing rows with FraccionamientoId set, not a separate entity — this keeps
// pricing, photos, and property-type fields in the one place that already owns them.
public class Fraccionamiento
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string? DeveloperName { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Free text on purpose (e.g. "Preventa", "En construcción", "Entrega inmediata") — not an
    // enum yet, since the real vocabulary each source uses hasn't been observed at scale. Revisit
    // once Fase 1-3 sources are actually producing data.
    public string? Stage { get; set; }

    public FraccionamientoStatus Status { get; set; } = FraccionamientoStatus.Candidate;

    public string? Description { get; set; }
    // Free-form JSON array of amenity labels (e.g. ["Alberca", "Casa club", "Seguridad 24h"]) —
    // an admin fills this in at approval time; no fixed catalog exists yet.
    public string? AmenitiesJson { get; set; }
    public string? MasterPlanImageUrl { get; set; }

    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public DateTime FirstDetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }

    // No navigation property on purpose, same reasoning as ErrorLog.ResolvedByUserId — this is an
    // audit trail, not a live relationship that should block or cascade off the account.
    public Guid? ReviewedByUserId { get; set; }

    // Set only when Status is MergedInto.
    public Guid? MergedIntoId { get; set; }

    public List<FraccionamientoSource> Sources { get; set; } = [];
    public List<Listing> Listings { get; set; } = [];
}
