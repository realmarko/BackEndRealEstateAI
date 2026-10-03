using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface ISaleProcessService
{
    // Adds (unverified) any checklist item from the target stage's document-template catalog
    // that this SaleProcess doesn't already have — called whenever a process is created or moved
    // to a new stage. Never removes or touches documents from stages already on the process, so
    // the full document trail across every stage the deal passed through stays visible.
    Task SyncStageDocumentsAsync(SaleProcess process, int stageId, CancellationToken cancellationToken = default);
}
