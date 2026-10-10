namespace RealEstate.Api.Services;

public interface IOwnerAgentLookupService
{
    // Batched: ListingsController.Search/AttachOwnerAgentDetailsAsync resolves a whole page of
    // listings' owners in one round trip.
    Task<Dictionary<Guid, (string? Company, string? PhotoUrl)>> GetManyAsync(IEnumerable<Guid> ownerIds);

    // Single-owner convenience over GetManyAsync — same cache, same query shape, so a one-off
    // caller (e.g. InquiriesController, resolving the owner for one listing at a time) never
    // drifts from the batched path's behavior.
    Task<(string? Company, string? PhotoUrl)> GetAsync(Guid ownerId);

    // Call after an Agent's Company/PhotoUrl changes (AgentsController.UpdateMine) or a listing's
    // owner changes (ListingsController's transfer flow) so stale cached info doesn't linger for
    // up to the cache's TTL.
    void Evict(Guid ownerId);
}
