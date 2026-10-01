using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IPopulationDensityService
{
    // Finds the AGEB (INEGI's smallest census geography) containing (lat, lng) and returns its
    // real 2020-census population and density. Returns null for a point outside any imported
    // AGEB — today that means anywhere outside the state(s) whose Marco Geoestadístico/Censo
    // data has been imported (Puebla only, initially), not necessarily an error.
    Task<PopulationDensityDto?> GetAsync(double lat, double lng, CancellationToken cancellationToken = default);
}

public class PopulationDensityService : IPopulationDensityService
{
    // 2020 census data never changes until a future census is imported (a deploy-time event,
    // not something that happens while the app is running), so this TTL only bounds a
    // cold-start worst case, same reasoning as the other reference-data caches.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    // Rounds the point to ~111m precision (3 decimal degrees) so the map's pan/zoom re-queries
    // of roughly the same spot share a cache entry instead of each needing an exact coordinate
    // match. AGEBs are typically several hundred meters to a kilometer across in urban areas, so
    // this can occasionally misattribute a point within ~111m of a real AGEB boundary to its
    // neighbor — an acceptable trade-off for a map "opportunity analysis" estimate, not a survey
    // tool, matching this feature's existing estimate-only framing (see SocioeconomicScore).
    private const int CoordinatePrecision = 3;

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public PopulationDensityService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<PopulationDensityDto?> GetAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"population-density:{Math.Round(lat, CoordinatePrecision)}:{Math.Round(lng, CoordinatePrecision)}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await FetchAsync(lat, lng, cancellationToken);
        });
    }

    private async Task<PopulationDensityDto?> FetchAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        // NetTopologySuite Point takes (x, y) i.e. (longitude, latitude) — swapping these would
        // silently look up the wrong hemisphere's worth of AGEBs for most of Mexico.
        var point = GeoFactory.Instance.CreatePoint(new Coordinate(lng, lat));

        var ageb = await _db.AgebPopulations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Boundary.Contains(point), cancellationToken);

        if (ageb is null) return null;

        return new PopulationDensityDto
        {
            Population = ageb.Population,
            AreaSqKm = ageb.AreaSqKm,
            DensityPerSqKm = ageb.AreaSqKm > 0 ? ageb.Population / ageb.AreaSqKm : 0,
            CensusYear = ageb.CensusYear,
            SocioeconomicScore = ageb.SocioeconomicScore,
            EstimatedSocioeconomicLevel = ageb.EstimatedSocioeconomicLevel?.ToString()
        };
    }
}
