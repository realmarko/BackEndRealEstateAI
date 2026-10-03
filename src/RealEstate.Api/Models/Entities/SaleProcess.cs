namespace RealEstate.Api.Models.Entities;

// One client's house-sale journey, from first contact through post-sale follow-up — the
// pipeline board's unit of work. Independent of Listing: a seller is often captured and walked
// through document/contract review before any public Listing exists, so ListingId is set only
// once one gets published for this property.
public class SaleProcess
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The agent responsible for this sale — an ApplicationUser with the Agent role, same
    // convention as Listing.OwnerId (no separate Agent-entity FK here: Agent lives in
    // RealEstateDbContext, see Agent.cs's own comment on why that link isn't EF-enforced).
    public Guid AgentUserId { get; set; }
    public ApplicationUser? AgentUser { get; set; }

    public Guid? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string PropertyAddress { get; set; } = string.Empty;
    public decimal EstimatedPrice { get; set; }

    public int CurrentStageId { get; set; }
    public PipelineStage? CurrentStage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SaleProcessDocument> Documents { get; set; } = new List<SaleProcessDocument>();
    public ICollection<SaleProcessTask> Tasks { get; set; } = new List<SaleProcessTask>();
}

// One checklist item for a SaleProcess, scoped to the stage it was required at — created
// (unverified) from PipelineStageDocumentTemplate when the process first reaches that stage, and
// kept even if the process later moves past that stage so the full document trail stays visible.
public class SaleProcessDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleProcessId { get; set; }
    public SaleProcess? SaleProcess { get; set; }

    public int StageId { get; set; }
    public PipelineStage? Stage { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// A free-form to-do the agent tracks against a SaleProcess, independent of the document
// checklist (e.g. "agendar firma con notaría") — added/removed manually, never seeded.
public class SaleProcessTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SaleProcessId { get; set; }
    public SaleProcess? SaleProcess { get; set; }

    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
