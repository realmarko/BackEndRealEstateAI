using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RealEstate.Api.Services;

public interface IDenueService
{
    // Counts businesses INEGI's DENUE registry has within radiusMeters of (lat, lng) whose
    // name, street, colonia, or economic-activity description matches searchTerm.
    Task<int> CountNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default);
}

public class DenueService : IDenueService
{
    // The Buscar (search) method — see DenueOptions for where the free token comes from.
    private const string BaseUrl = "https://www.inegi.org.mx/app/api/denue/v1/consulta/Buscar";

    private readonly HttpClient _http;
    private readonly DenueOptions _options;

    public DenueService(HttpClient http, IOptions<DenueOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<int> CountNearbyAsync(string searchTerm, double lat, double lng, int radiusMeters, CancellationToken cancellationToken = default)
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
            return 0;

        // Only the count is needed here — parsing into the full 22-field-per-establishment
        // shape DENUE returns would be several times the code for no benefit yet. If a future
        // feature needs the establishments themselves (names, addresses), that's the point to
        // add a proper response model.
        using var document = JsonDocument.Parse(body);

        // A successful (2xx) response that isn't the documented result array is DENUE reporting
        // some other problem in its own body instead of via HTTP status — e.g. an invalid token
        // (see above) or a JSON error object. Treating it as a thrown failure — not a silent 0 —
        // matters because the caller labels a successful call "Source: INEGI DENUE (official
        // registry)": reporting "0 competitors, officially" for what was actually a failed lookup
        // would be confidently wrong.
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("DENUE returned an unexpected response shape.");

        return document.RootElement.GetArrayLength();
    }
}
