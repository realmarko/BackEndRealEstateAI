using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IFraccionamientoIngestionService
{
    Task<IngestCandidatesResultDto> IngestAsync(IReadOnlyList<FraccionamientoCandidateDto> candidates, CancellationToken cancellationToken = default);

    // Same dedup-against-existing-records logic as IngestAsync, for a single candidate where the
    // caller needs the affected Fraccionamiento back (e.g. the admin "add manually" form wants to
    // land on the record it just created or matched) rather than just a count.
    Task<(Fraccionamiento Fraccionamiento, bool MatchedExisting)> IngestOneAsync(FraccionamientoCandidateDto candidate, CancellationToken cancellationToken = default);
}
