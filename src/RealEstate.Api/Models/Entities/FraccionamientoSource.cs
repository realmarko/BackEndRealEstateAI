namespace RealEstate.Api.Models.Entities;

public enum FraccionamientoSourceType
{
    Ruv = 0,
    Lamudi = 1,
    Inmuebles24 = 2,
    Vivanuncios = 3,
    DeveloperSite = 4,
    MunicipalGazette = 5,
    Satellite = 6,
    // An admin typed this in directly (features/admin/admin-fraccionamientos' "add manually"
    // form) instead of it arriving through the n8n ingestion pipeline — kept as a real source so
    // it still shows up in the audit trail and still participates in the same dedup logic as
    // every scraped source.
    ManualAdmin = 7
}

// One row per external detection that contributed to a Fraccionamiento — a development can (and
// usually will) show up in more than one source, e.g. once in RUV and again on a portal. The
// ingestion endpoint's deduplication logic is what decides whether an incoming detection attaches
// a new row here to an existing Fraccionamiento or creates a new one.
public class FraccionamientoSource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FraccionamientoId { get; set; }
    public Fraccionamiento? Fraccionamiento { get; set; }

    public FraccionamientoSourceType SourceType { get; set; }
    public string? SourceUrl { get; set; }

    // Untouched snapshot of whatever the source returned, for auditing/debugging a bad match —
    // not surfaced to end users.
    public string? RawDataJson { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
