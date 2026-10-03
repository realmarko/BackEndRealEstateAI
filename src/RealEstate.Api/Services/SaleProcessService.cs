using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public class SaleProcessService : ISaleProcessService
{
    private readonly ApplicationDbContext _db;

    public SaleProcessService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task SyncStageDocumentsAsync(SaleProcess process, int stageId, CancellationToken cancellationToken = default)
    {
        var templates = await _db.PipelineStageDocumentTemplates
            .Where(t => t.StageId == stageId)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);
        if (templates.Count == 0) return;

        var existingNames = process.Documents
            .Where(d => d.StageId == stageId)
            .Select(d => d.Name)
            .ToHashSet();

        foreach (var template in templates)
        {
            if (existingNames.Contains(template.Name)) continue;
            var document = new SaleProcessDocument
            {
                SaleProcessId = process.Id,
                StageId = stageId,
                Name = template.Name
            };
            // Added via the DbSet, not process.Documents.Add(...): this entity's Guid key is
            // already set (SaleProcessDocument.Id's property initializer), so when the owning
            // SaleProcess is an already-tracked (Unchanged/Modified) entity rather than brand new,
            // EF's default convention reads that pre-set key as "this row already exists" and
            // issues an UPDATE instead of an INSERT — which then fails as a concurrency exception
            // because no such row exists yet. Marking it Added bypasses that key-based guess; EF's
            // own relationship fixup (matching SaleProcessId) adds it to process.Documents too, so
            // adding it there a second time would just duplicate the in-memory entry.
            _db.SaleProcessDocuments.Add(document);
        }
    }
}
