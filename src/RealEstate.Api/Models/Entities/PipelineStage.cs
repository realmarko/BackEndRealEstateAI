namespace RealEstate.Api.Models.Entities;

// Fixed catalog of the sale pipeline's steps — seeded once via HasData (see
// ApplicationDbContext.OnModelCreating) and never user-editable at runtime. SortOrder, not Id,
// drives display order so stages could be reordered without renumbering.
public class PipelineStage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Flags a stage as a contract-review checkpoint (exclusivity contract or compraventa) — the
    // frontend highlights these so the agent knows legal sign-off is expected at this step.
    public bool RequiresLegalReview { get; set; }

    public ICollection<PipelineStageDocumentTemplate> DocumentTemplates { get; set; } = new List<PipelineStageDocumentTemplate>();
}

// The default checklist item names for a given stage — copied into SaleProcessDocument rows
// (unverified) the first time a SaleProcess reaches that stage. Editing this catalog changes
// what gets added for sale processes that haven't reached the stage yet; it never retroactively
// touches documents already created for processes already past it.
public class PipelineStageDocumentTemplate
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public PipelineStage? Stage { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
