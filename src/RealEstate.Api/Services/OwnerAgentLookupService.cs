using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Data;

namespace RealEstate.Api.Services;

// Single home for "resolve a listing owner's Agent.Company/PhotoUrl" — previously duplicated
// between ListingsController.AttachOwnerAgentDetailsAsync (batched, listing-page-scale traffic)
// and InquiriesController (one owner per qualifying inquiry) as two independent queries that
// could silently drift apart. Neither field is caller-specific, so every owner's entry is simply
// shared by everyone — same cache, same 5-minute TTL, regardless of which caller asks first.
public class OwnerAgentLookupService : IOwnerAgentLookupService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly RealEstateDbContext _agentsDb;
    private readonly IMemoryCache _cache;

    public OwnerAgentLookupService(RealEstateDbContext agentsDb, IMemoryCache cache)
    {
        _agentsDb = agentsDb;
        _cache = cache;
    }

    private static string CacheKey(Guid ownerId) => $"owner-agent-info:{ownerId}";

    public async Task<Dictionary<Guid, (string? Company, string? PhotoUrl)>> GetManyAsync(IEnumerable<Guid> ownerIds)
    {
        var distinctIds = ownerIds.Distinct().ToList();
        if (distinctIds.Count == 0) return new Dictionary<Guid, (string?, string?)>();

        var resolved = new Dictionary<Guid, (string? Company, string? PhotoUrl)>();
        var missingIds = new List<Guid>();
        foreach (var ownerId in distinctIds)
        {
            if (_cache.TryGetValue(CacheKey(ownerId), out (string? Company, string? PhotoUrl) cached))
                resolved[ownerId] = cached;
            else
                missingIds.Add(ownerId);
        }

        if (missingIds.Count > 0)
        {
            // ToDictionaryAsync's key/value selectors run against already-materialized Agent
            // entities, not translated to SQL, so Company (sourced from Brokerage.Name) needs the
            // navigation eager-loaded here or it would read back null for every agent.
            var agents = await _agentsDb.Agents
                .Include(a => a.Brokerage)
                .Where(a => a.UserId != null && missingIds.Contains(a.UserId.Value))
                .ToDictionaryAsync(a => a.UserId!.Value, a => (a.Company, a.PhotoUrl));

            foreach (var ownerId in missingIds)
            {
                // An owner with no matching Agent (a plain Owner-role user, not also an Agent)
                // caches as the same default (null, null) as an agent with no company/photo set —
                // identical either way to a caller, and caching it too avoids re-querying for the
                // same non-agent owner on every subsequent lookup.
                var info = agents.GetValueOrDefault(ownerId);
                resolved[ownerId] = info;
                _cache.Set(CacheKey(ownerId), info, CacheDuration);
            }
        }

        return resolved;
    }

    public async Task<(string? Company, string? PhotoUrl)> GetAsync(Guid ownerId)
    {
        var results = await GetManyAsync([ownerId]);
        return results.GetValueOrDefault(ownerId);
    }

    public void Evict(Guid ownerId) => _cache.Remove(CacheKey(ownerId));
}
