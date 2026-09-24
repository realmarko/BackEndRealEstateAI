using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

// First-pass deduplication for POST /api/fraccionamientos/candidates: a development usually gets
// detected more than once (RUV today, a portal listing next week, ...), and the point of this
// service is deciding "is this the same physical development we already know about, or a new
// one" before either attaching a source to an existing Fraccionamiento or creating a fresh
// Candidate row. Deliberately simple (a fixed radius + a name-similarity ratio) — the plan's own
// "Fase 4: deduplicación" is exactly about refining this once real multi-source data exists to
// tune against; there's nothing to tune yet with zero real detections.
public class FraccionamientoIngestionService : IFraccionamientoIngestionService
{
    private const double MatchRadiusMeters = 500;
    private const double NameSimilarityThreshold = 0.6;
    private const double EarthRadiusMeters = 6_371_000;

    private readonly ApplicationDbContext _db;

    public FraccionamientoIngestionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IngestCandidatesResultDto> IngestAsync(IReadOnlyList<FraccionamientoCandidateDto> candidates, CancellationToken cancellationToken = default)
    {
        var result = new IngestCandidatesResultDto();

        // Fraccionamiento rows created earlier in this same batch, not yet saved — SaveChangesAsync
        // only runs once, after the whole loop, so FindMatchAsync's own EF query (which hits
        // Postgres directly) can never see them. Tracked here and checked alongside the DB query
        // so two sources for one brand-new development scraped in the same n8n run still land on
        // a single Fraccionamiento instead of creating two duplicates.
        var pendingNew = new List<Fraccionamiento>();

        // Sequential, not parallel: candidates in the same batch can themselves be duplicates of
        // each other, and a EF Core DbContext isn't safe to use concurrently anyway.
        foreach (var candidate in candidates)
        {
            var (_, matchedExisting) = await IngestOneInternalAsync(candidate, pendingNew, cancellationToken);
            if (matchedExisting) result.MatchedExisting++; else result.Created++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<(Fraccionamiento Fraccionamiento, bool MatchedExisting)> IngestOneAsync(FraccionamientoCandidateDto candidate, CancellationToken cancellationToken = default)
    {
        var result = await IngestOneInternalAsync(candidate, new List<Fraccionamiento>(), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<(Fraccionamiento Fraccionamiento, bool MatchedExisting)> IngestOneInternalAsync(
        FraccionamientoCandidateDto candidate, List<Fraccionamiento> pendingNew, CancellationToken cancellationToken)
    {
        var match = await FindMatchAsync(candidate, pendingNew, cancellationToken);

        if (match is not null)
        {
            // Explicit _db.Add() + FK, not match.Sources.Add(...): appending to a navigation
            // collection on an already-tracked parent leaves EF Core to guess whether the new
            // item is an insert or an update to an existing row, and with a client-generated
            // Guid key (not a default value it can treat as "obviously new") it guesses wrong
            // — confirmed live: it emitted an UPDATE with a WHERE id = <the new row's id>,
            // which matched zero rows and threw DbUpdateConcurrencyException. Adding via the
            // DbSet directly leaves no ambiguity about the entity's state.
            var source = BuildSource(candidate);
            source.FraccionamientoId = match.Id;
            _db.FraccionamientoSources.Add(source);
            return (match, true);
        }

        var fraccionamiento = new Fraccionamiento
        {
            Name = candidate.Name,
            DeveloperName = candidate.DeveloperName,
            City = candidate.City,
            State = candidate.State,
            Latitude = candidate.Latitude,
            Longitude = candidate.Longitude,
            Stage = candidate.Stage
        };
        fraccionamiento.Sources.Add(BuildSource(candidate));
        _db.Fraccionamientos.Add(fraccionamiento);
        pendingNew.Add(fraccionamiento);
        return (fraccionamiento, false);
    }

    private static FraccionamientoSource BuildSource(FraccionamientoCandidateDto candidate) => new()
    {
        SourceType = candidate.SourceType,
        SourceUrl = candidate.SourceUrl,
        RawDataJson = candidate.RawData
    };

    private async Task<Fraccionamiento?> FindMatchAsync(FraccionamientoCandidateDto candidate, IReadOnlyList<Fraccionamiento> pendingNew, CancellationToken cancellationToken)
    {
        // A merged-away record is never itself a match target — MergedIntoId points at the
        // survivor a caller should have matched against instead.
        var excludedStatus = FraccionamientoStatus.MergedInto;

        // Coarse bounding-box pre-filter in SQL (cheap, index-backed via the Latitude/Longitude
        // index) narrows this to a handful of rows at most before the precise haversine + name
        // check below runs in memory — a real distance calculation isn't translatable to SQL here
        // without PostGIS geography columns, which Fraccionamiento doesn't use.
        var latDelta = MatchRadiusMeters / 111_000.0;
        var lngDelta = MatchRadiusMeters / (111_000.0 * Math.Cos(candidate.Latitude * Math.PI / 180));

        // No .Include(f => f.Sources): the match itself only needs Id/Latitude/Longitude/Name,
        // and the new source is attached via _db.FraccionamientoSources.Add(...) with the FK set
        // explicitly (see IngestAsync) rather than through this collection — loading every
        // existing source for every nearby candidate here would be pure waste.
        var nearby = await _db.Fraccionamientos
            .Where(f => f.Status != excludedStatus)
            .Where(f => f.Latitude >= candidate.Latitude - latDelta && f.Latitude <= candidate.Latitude + latDelta)
            .Where(f => f.Longitude >= candidate.Longitude - lngDelta && f.Longitude <= candidate.Longitude + lngDelta)
            .ToListAsync(cancellationToken);

        // pendingNew covers same-batch duplicates the DB query above can't see yet (see the
        // comment on IngestAsync's pendingNew) — checked with the exact same distance/name logic.
        return nearby.Concat(pendingNew).FirstOrDefault(f =>
            DistanceMeters(f.Latitude, f.Longitude, candidate.Latitude, candidate.Longitude) <= MatchRadiusMeters &&
            NameSimilarity(f.Name, candidate.Name) >= NameSimilarityThreshold);
    }

    private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        double ToRadians(double deg) => deg * Math.PI / 180;

        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    // 1 - (edit distance / longer string's length): 1.0 for identical strings, 0.0 for
    // completely unrelated ones. Normalizes case and accents first so "Peña" vs "Pena" or
    // "RESIDENCIAL" vs "Residencial" — encoding/formatting noise between sources, not a real
    // difference — don't get penalized.
    private static double NameSimilarity(string a, string b)
    {
        var normalizedA = Normalize(a);
        var normalizedB = Normalize(b);
        if (normalizedA.Length == 0 || normalizedB.Length == 0) return 0;

        var distance = LevenshteinDistance(normalizedA, normalizedB);
        var longerLength = Math.Max(normalizedA.Length, normalizedB.Length);
        return 1.0 - (double)distance / longerLength;
    }

    private static string Normalize(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = formD.Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return new string(chars.ToArray()).Normalize(System.Text.NormalizationForm.FormC);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var distances = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) distances[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) distances[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
    }
}
