using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

// One DENUE establishment — real field names confirmed against a live response (DENUE's JSON
// keys: Nombre, Calle, Colonia, Longitud, Latitud; see DenueService.ParseEstablishments).
public record DenueEstablishment(string Name, string? Street, string? Colonia, double Lat, double Lng);

public interface IDenueService
{
    // Counts businesses INEGI's DENUE registry has within radiusMeters of (lat, lng) whose
    // name, street, colonia, or economic-activity description matches searchTerm.
    Task<int> CountNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);

    // Same search, with the establishments themselves (name/address/coordinates) instead of just
    // a count — used where a visitor benefits from seeing who the competitors/nearby anchors
    // actually are (e.g. "Farmacias Guadalajara, 650 m"), not just how many. Shares the exact same
    // cache entry as CountNearbyAsync for the same (searchTerm, lat, lng, radiusMeters), so asking
    // for both costs one DENUE call, not two.
    Task<List<DenueEstablishment>> ListNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);
}

public class DenueService : IDenueService
{
    // The Buscar (search) method — see DenueOptions for where the free token comes from.
    private const string BaseUrl = "https://www.inegi.org.mx/app/api/denue/v1/consulta/Buscar";

    // Business registrations don't open/close minute-to-minute, and two map clicks "near" the
    // same spot are a common pattern (zooming in, re-clicking a popular area) — caching avoids
    // burning this app's own rate-limited INEGI token on a lookup another visitor already paid for.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    // Rounds the point to ~111m precision (3 decimal degrees) so nearby clicks for the same
    // search share a cache entry instead of each needing an exact coordinate match — coarse
    // enough to get real hit-rate, fine enough that it doesn't meaningfully change which
    // businesses fall within a multi-hundred-meter radiusMeters search.
    private const int CoordinatePrecision = 3;

    private readonly HttpClient _http;
    private readonly DenueOptions _options;
    private readonly IMemoryCache _cache;

    public DenueService(HttpClient http, IOptions<DenueOptions> options, IMemoryCache cache)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<int> CountNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
        => (await ListNearbyAsync(searchTerm, lat, lng, radiusMeters, cancellationToken)).Count;

    public async Task<List<DenueEstablishment>> ListNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"denue:{searchTerm.ToLowerInvariant()}:{Math.Round(lat, CoordinatePrecision)}:{Math.Round(lng, CoordinatePrecision)}:{radiusMeters}";

        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await FetchEstablishmentsAsync(searchTerm, lat, lng, radiusMeters, cancellationToken);
        }) ?? new List<DenueEstablishment>();
    }

    private async Task<List<DenueEstablishment>> FetchEstablishmentsAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken)
    {
        // Invariant culture: a comma-as-decimal-separator culture would silently turn
        // "19.04" into "19,04", corrupting the lat,lng path segment DENUE expects.
        var latLng = $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
        var url = $"{BaseUrl}/{Uri.EscapeDataString(searchTerm)}/{latLng}/{radiusMeters}/{_options.Token}";

        using var response = await _http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        // DENUE reports some conditions as plain text instead of JSON, still under a 2xx status:
        // an invalid token returns "No Autorizado, utilice una clave valida.", and (verified live
        // with a real token) a genuinely empty result set returns "No hay resultados." rather than
        // "[]". Read as text first so the empty-results case can be told apart from a real failure
        // instead of both failing JsonDocument.Parse the same way.
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.Equals(body.Trim(), "No hay resultados.", StringComparison.OrdinalIgnoreCase))
            return new List<DenueEstablishment>();

        using var document = JsonDocument.Parse(body);

        // A successful (2xx) response that isn't the documented result array is DENUE reporting
        // some other problem in its own body instead of via HTTP status — e.g. an invalid token
        // (see above) or a JSON error object. Treating it as a thrown failure — not a silent empty
        // list — matters because the caller labels a successful call "Source: INEGI DENUE (official
        // registry)": reporting "0 competitors, officially" for what was actually a failed lookup
        // would be confidently wrong. It also must not be cached — GetOrCreateAsync only caches
        // a factory that completes normally, so a thrown exception here correctly leaves no entry
        // behind for the next caller to retry cleanly.
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("DENUE returned an unexpected response shape.");

        return ParseEstablishments(document.RootElement);
    }

    // Confirmed field names/casing against a live DENUE response (not just secondhand docs):
    // Nombre, Calle, Colonia, Longitud, Latitud, among ~22 fields total — only the ones this app
    // actually uses are read here. Longitud/Latitud come through as JSON strings, not numbers.
    private static List<DenueEstablishment> ParseEstablishments(JsonElement array)
    {
        var results = new List<DenueEstablishment>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            var name = item.TryGetProperty("Nombre", out var nameEl) ? nameEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var lat = TryParseDouble(item, "Latitud");
            var lng = TryParseDouble(item, "Longitud");
            if (lat is null || lng is null) continue;

            var street = item.TryGetProperty("Calle", out var streetEl) ? streetEl.GetString() : null;
            var colonia = item.TryGetProperty("Colonia", out var coloniaEl) ? coloniaEl.GetString() : null;

            results.Add(new DenueEstablishment(name, NullIfNinguno(street), NullIfNinguno(colonia), lat.Value, lng.Value));
        }
        return results;
    }

    // DENUE uses the literal string "NINGUNO" for an unknown street/colonia rather than omitting
    // the field — surfacing that verbatim ("Calle: NINGUNO") would read as a data error, not as
    // "this field is blank".
    private static string? NullIfNinguno(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "NINGUNO", StringComparison.OrdinalIgnoreCase) ? null : value;

    private static double? TryParseDouble(JsonElement item, string property) =>
        item.TryGetProperty(property, out var el) && el.GetString() is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
