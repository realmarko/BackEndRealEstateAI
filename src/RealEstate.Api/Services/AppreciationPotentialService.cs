using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IAppreciationPotentialService
{
    // One AGEB's "potencial de plusvalía (2 años)" score, built from leading indicators already
    // available in this app rather than a real historical price series (none exists yet — see
    // the plan this was built from). Cached per AGEB: see AppreciationPotentialService.CacheDuration.
    Task<AppreciationPotentialDto> ScoreAgebAsync(AgebPopulation ageb, CancellationToken cancellationToken = default);
}

public class AppreciationPotentialService : IAppreciationPotentialService
{
    // Same radius ViabilityAnalysisService.NearbyFraccionamientoRadiusMeters uses for "a new
    // development's demand reaches this far" — kept consistent rather than inventing a second
    // number for the same real-world distance.
    private const double NearbyFraccionamientoRadiusMeters = 2000;

    // AGEBs are "a few city blocks" (see AgebPopulation's own comment) — a neighbor within this
    // radius of the target's centroid is "next door", not "the same general area".
    private const double NeighborAgebRadiusMeters = 700;

    private const double ConnectivityRadiusMeters = 1000;

    // IRoadNetworkService retries across 3 Overpass mirrors on failure, each with its own 10s
    // HttpClient timeout (see RoadNetworkService.cs) — worst case ~30s for ONE of roads/transit if
    // every mirror is slow that day. Both api-dev and api sit behind CloudFront with a 30-60s
    // OriginReadTimeout, and GeomarketingController scores up to MaxAppreciationPotentialAgebs
    // AGEBs through an 8-wide semaphore, so several AGEBs each eating a ~30s worst case would blow
    // well past that budget. This bounds the WHOLE connectivity signal (both roads and transit
    // together, already run in parallel) to a hard ceiling, independent of RoadNetworkService's
    // own per-mirror retry budget — past this, the signal is simply excluded (see the catch below),
    // same as any other signal with insufficient data.
    private static readonly TimeSpan ConnectivitySignalTimeout = TimeSpan.FromSeconds(6);

    // Below this many Active listings inside the AGEB's own boundary in the trailing 12 months,
    // a price/sqft trend is noise, not a signal — excluded (not scored as 0) same as every other
    // signal here when its underlying data is too thin to mean anything.
    private const int MinListingsPerHalfForMomentum = 3;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    // Process-wide (static, not per-request), not per-request: GeomarketingController can fire up
    // to MaxAppreciationPotentialAgebs ComputeAsync calls at once for ONE request, and several
    // visitors can hit the endpoint at the same moment — without a shared cap, that fan-out (each
    // call opens its own pooled DbContext and up to 2 live Overpass HTTP calls) risks exhausting
    // the Npgsql connection pool and tripping Overpass's documented "at capacity" 502s (see
    // RoadNetworkService's own comments) well before any individual request's own cap kicks in.
    private static readonly SemaphoreSlim ConcurrencyGate = new(8, 8);

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IRoadNetworkService _roadNetworkService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AppreciationPotentialService> _logger;

    public AppreciationPotentialService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IRoadNetworkService roadNetworkService,
        IMemoryCache cache,
        ILogger<AppreciationPotentialService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _roadNetworkService = roadNetworkService;
        _cache = cache;
        _logger = logger;
    }

    public Task<AppreciationPotentialDto> ScoreAgebAsync(AgebPopulation ageb, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"appreciation-potential:{ageb.Cvegeo}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await ComputeAsync(ageb, cancellationToken);
        })!;
    }

    private async Task<AppreciationPotentialDto> ComputeAsync(AgebPopulation ageb, CancellationToken cancellationToken)
    {
        await ConcurrencyGate.WaitAsync(cancellationToken);
        try
        {
            return await ComputeCoreAsync(ageb, cancellationToken);
        }
        finally
        {
            ConcurrencyGate.Release();
        }
    }

    private async Task<AppreciationPotentialDto> ComputeCoreAsync(AgebPopulation ageb, CancellationToken cancellationToken)
    {
        var centroid = ageb.Boundary.Centroid;
        var lat = centroid.Y;
        var lng = centroid.X;

        // One short-lived context per AGEB scored (see IDbContextFactory above) — ScoreAgebAsync
        // runs in parallel across AGEBs (GeomarketingController's Task.WhenAll), and a single
        // DbContext instance can't serve concurrent operations.
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // (score 0-100, weight, explanation) — a signal that couldn't be computed for this AGEB
        // is simply never added here, which is how it drops out of both the weighted average and
        // the explanations list (same "excluded, not penalized" idea as
        // ViabilityAnalysisService's null-Passed checks).
        var signals = new List<(double Score, double Weight, string Explanation)>();

        await AddNewDevelopmentSignalAsync(db, lat, lng, signals, cancellationToken);
        await AddSocioeconomicConvergenceSignal(db, ageb, lat, lng, signals, cancellationToken);
        await AddConnectivitySignalAsync(lat, lng, signals, cancellationToken);
        await AddMarketMomentumSignalAsync(db, ageb, signals, cancellationToken);

        double? finalScore = null;
        if (signals.Count > 0)
        {
            var totalWeight = signals.Sum(s => s.Weight);
            finalScore = Math.Round(signals.Sum(s => s.Score * s.Weight) / totalWeight, 1);
        }

        return new AppreciationPotentialDto
        {
            Cvegeo = ageb.Cvegeo,
            Score = finalScore,
            Explanations = signals.Select(s => s.Explanation).ToList()
        };
    }

    // Weight 35: a nearby new residential development is the clearest "something is about to
    // change here" signal available without a real historical price series.
    private async Task AddNewDevelopmentSignalAsync(ApplicationDbContext db, double lat, double lng, List<(double Score, double Weight, string Explanation)> signals, CancellationToken cancellationToken)
    {
        // Shared with ViabilityAnalysisService's "nuevo desarrollo residencial cercano" check —
        // see FraccionamientoProximity. Already sorted by ascending distance.
        var nearby = await FraccionamientoProximity.FindNearbyAsync(db, lat, lng, NearbyFraccionamientoRadiusMeters, cancellationToken);

        if (nearby.Count == 0) return;

        // A development still in preventa/construcción represents demand that hasn't landed yet
        // (more of the appreciation runway still ahead); one already in "entrega inmediata" has
        // already delivered most of its demand impact, so it counts for less here. Stage is a
        // free-text admin field (no enum/dropdown backs it), so this matches case/whitespace-
        // insensitively rather than exactly — an admin typing "entrega inmediata" or "Entrega
        // Inmediata " shouldn't silently fall through to the full weight.
        double StageWeight(string? stage) => stage?.Trim().Equals("Entrega inmediata", StringComparison.OrdinalIgnoreCase) == true ? 0.5 : 1.0;
        var effectiveCount = nearby.Sum(f => StageWeight(f.Stage));

        var score = Math.Clamp(effectiveCount / 3.0 * 100, 0, 100);
        var closest = nearby[0];
        var explanation = nearby.Count == 1
            ? $"1 desarrollo nuevo cercano: \"{closest.Name}\" a {closest.DistanceMeters:N0} m."
            : $"{nearby.Count} desarrollos nuevos dentro de {NearbyFraccionamientoRadiusMeters:N0} m, el más cercano \"{closest.Name}\" a {closest.DistanceMeters:N0} m.";

        signals.Add((score, 35, explanation));
    }

    // Weight 25: an AGEB whose immediate neighbors already score higher socioeconomically has
    // room to converge upward; one whose neighbors score lower has already reached (or passed)
    // the local ceiling.
    private async Task AddSocioeconomicConvergenceSignal(ApplicationDbContext db, AgebPopulation ageb, double lat, double lng, List<(double Score, double Weight, string Explanation)> signals, CancellationToken cancellationToken)
    {
        if (ageb.SocioeconomicScore is not { } ownScore) return;

        // A bbox-around-the-centroid prefilter only, same role as the one in
        // AddNewDevelopmentSignalAsync — the actual "within NeighborAgebRadiusMeters" cut below
        // is centroid-to-centroid distance, not Boundary.Intersects(bbox). Testing a neighbor's
        // (possibly large/irregular) polygon against a small box around THIS AGEB's centroid can
        // both miss a true neighbor whose own centroid sits further than its polygon's nearest
        // edge, and catch an unrelated AGEB via a distant protrusion of its shape.
        var (latDelta, lngDelta) = GeoFactory.ApproxDegreeDelta(lat, NeighborAgebRadiusMeters);
        var bbox = GeoFactory.CreateBoundingBox(lat - latDelta, lng - lngDelta, lat + latDelta, lng + lngDelta);

        // Boundary materialized (not Select-projected to .Centroid) because NetTopologySuite's
        // Centroid isn't one of the spatial members Npgsql translates to SQL — same reason
        // GeomarketingController.GetCompetitorDensity computes centroids in memory, after
        // ToListAsync, rather than inside the query.
        var candidates = await db.AgebPopulations.AsNoTracking()
            .Where(a => a.Cvegeo != ageb.Cvegeo)
            .Where(a => a.Boundary.Intersects(bbox))
            .Where(a => a.SocioeconomicScore != null)
            .Select(a => new { a.SocioeconomicScore, a.Boundary })
            .ToListAsync(cancellationToken);

        var neighborScores = candidates
            .Select(a => new { a.SocioeconomicScore, Centroid = a.Boundary.Centroid })
            .Where(a => GeoFactory.DistanceMeters(lat, lng, a.Centroid.Y, a.Centroid.X) <= NeighborAgebRadiusMeters)
            .Select(a => a.SocioeconomicScore!.Value)
            .ToList();

        if (neighborScores.Count == 0) return;

        var neighborAvg = neighborScores.Average();
        var delta = neighborAvg - ownScore;
        var score = Math.Clamp(50 + delta, 0, 100);

        signals.Add((score, 25, delta > 0
            ? $"Nivel socioeconómico {delta:N0} pts por debajo del promedio de sus {neighborScores.Count} AGEB vecinos — margen para converger hacia arriba."
            : $"Nivel socioeconómico ya en línea o por encima del promedio de sus {neighborScores.Count} AGEB vecinos."));
    }

    // Weight 15: existing major-road/transit connectivity. Reuses OverpassRoadNetworkService's
    // own 24h cache (see RoadNetworkService.cs), so repeat requests for the same area are cheap —
    // only a genuinely new area pays the live Overpass cost.
    private async Task AddConnectivitySignalAsync(double lat, double lng, List<(double Score, double Weight, string Explanation)> signals, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ConnectivitySignalTimeout);

        try
        {
            var roadsTask = _roadNetworkService.FindMajorRoadsNearbyAsync(lat, lng, (int)ConnectivityRadiusMeters, timeoutCts.Token);
            var transitTask = _roadNetworkService.FindTransitNearbyAsync(lat, lng, (int)ConnectivityRadiusMeters, timeoutCts.Token);
            await Task.WhenAll(roadsTask, transitTask);

            var hasRoad = roadsTask.Result.Count > 0;
            var hasTransit = transitTask.Result.Count > 0;
            double score = (hasRoad, hasTransit) switch
            {
                (true, true) => 100,
                (true, false) or (false, true) => 50,
                _ => 0
            };

            var explanation = (hasRoad, hasTransit) switch
            {
                (true, true) => "Vialidad principal y transporte público a menos de 1 km.",
                (true, false) => "Vialidad principal a menos de 1 km, sin transporte público cercano.",
                (false, true) => "Transporte público a menos de 1 km, sin vialidad principal cercana.",
                _ => "Sin vialidad principal ni transporte público a menos de 1 km."
            };

            signals.Add((score, 15, explanation));
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // Overpass's free mirrors are occasionally all unavailable at once (see
            // RoadNetworkService's own comments), or ConnectivitySignalTimeout above cut this off
            // before they answered — either way, excluded like any other signal with no data, not
            // scored as 0 (which would wrongly read as "no connectivity"). Catches
            // OperationCanceledException rather than just its TaskCanceledException subclass,
            // since both our own timeoutCts and the underlying stream read can surface either
            // depending on exactly where the cancellation is observed.
            _logger.LogWarning(ex, "Connectivity signal unavailable or timed out at {Lat},{Lng}", lat, lng);
        }
    }

    // Weight 25: price/sqft momentum already visible in this app's own listing data for the
    // zone — the one signal that reflects the market directly rather than a proxy for it.
    private async Task AddMarketMomentumSignalAsync(ApplicationDbContext db, AgebPopulation ageb, List<(double Score, double Weight, string Explanation)> signals, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var midpoint = DateTime.UtcNow.AddMonths(-6);

        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active && !l.PriceOnRequest && l.Price > 0 && l.AreaSqFt > 0)
            .Where(l => l.CreatedAt >= cutoff)
            .Where(l => l.Location != null && l.Location.Within(ageb.Boundary))
            .Select(l => new { l.Price, l.AreaSqFt, l.CreatedAt })
            .ToListAsync(cancellationToken);

        var older = listings.Where(l => l.CreatedAt < midpoint).ToList();
        var recent = listings.Where(l => l.CreatedAt >= midpoint).ToList();
        if (older.Count < MinListingsPerHalfForMomentum || recent.Count < MinListingsPerHalfForMomentum) return;

        var olderAvg = older.Average(l => (double)l.Price / l.AreaSqFt);
        var recentAvg = recent.Average(l => (double)l.Price / l.AreaSqFt);
        if (olderAvg <= 0) return;

        var pctChange = (recentAvg - olderAvg) / olderAvg;
        // A ±25% swing in price/sqft between halves maps to the full 0-100 range — wide enough
        // that a single outlier listing doesn't saturate the score, narrow enough that a real
        // double-digit swing still moves it meaningfully.
        var score = Math.Clamp(50 + pctChange * 200, 0, 100);

        signals.Add((score, 25, $"Precio/sqft {(pctChange >= 0 ? "subió" : "bajó")} {Math.Abs(pctChange):P0} en los últimos 6 meses vs. los 6 anteriores ({older.Count + recent.Count} publicaciones)."));
    }
}
