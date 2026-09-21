using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

// Location-intelligence endpoints for the map's "opportunity analysis" tools (see
// map-view.component.ts). Read-only and open to anonymous visitors, same as browsing the map
// itself — rate-limited instead, since each call proxies a real request to an external API.
[ApiController]
[Route("api/geomarketing")]
[EnableRateLimiting("geomarketing")]
public class GeomarketingController : ControllerBase
{
    // Must stay in sync with the frontend's OPPORTUNITY_CATEGORIES (map-view.component.ts) —
    // without this, the endpoint would forward any caller-supplied text straight to INEGI,
    // turning this app's own token and rate-limit budget into a free, open DENUE search proxy
    // for anyone who finds the URL, not just the map feature this exists for.
    private static readonly HashSet<string> AllowedSearchTerms = new(StringComparer.OrdinalIgnoreCase) { "farmacia", "gimnasio", "oxxo" };

    private readonly IDenueService _denueService;
    private readonly IPopulationDensityService _populationDensityService;
    private readonly ILogger<GeomarketingController> _logger;

    public GeomarketingController(
        IDenueService denueService,
        IPopulationDensityService populationDensityService,
        ILogger<GeomarketingController> logger)
    {
        _denueService = denueService;
        _populationDensityService = populationDensityService;
        _logger = logger;
    }

    // GET /api/geomarketing/business-density?searchTerm=farmacia&lat=19.04&lng=-98.20&radiusMeters=1000
    // Counts INEGI DENUE-registered businesses matching searchTerm within radiusMeters of the
    // point — official-registry counterpart to (not a replacement for) the frontend's own
    // Google Places Nearby Search, which the caller combines with this result.
    [HttpGet("business-density")]
    public async Task<ActionResult<BusinessDensityDto>> BusinessDensity(
        [FromQuery] string searchTerm,
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int radiusMeters)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || !AllowedSearchTerms.Contains(searchTerm))
            return BadRequest(new { message = "searchTerm is not a supported business category." });

        var latLngError = GeoValidation.ValidateLatLng(lat, lng);
        if (latLngError is not null) return BadRequest(new { message = latLngError });

        // Same upper bound as the frontend's own opportunity-analysis radius, generously
        // doubled — nothing legitimate needs a much wider single query, and DENUE's result set
        // (and this endpoint's response time) grows with the search area.
        if (radiusMeters is <= 0 or > 5000)
            return BadRequest(new { message = "radiusMeters must be between 1 and 5000." });

        try
        {
            var count = await _denueService.CountNearbyAsync(searchTerm, lat, lng, radiusMeters);
            return Ok(new BusinessDensityDto { Count = count });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // Expected until a real DENUE token replaces DenueOptions' CHANGE_ME placeholder
            // (INEGI's API rejects an invalid token), and possible any time INEGI's service
            // itself is unreachable or returns something other than the documented result array
            // — the caller falls back to its own Places-only count either way, so this never
            // needs to be more than a 502 and a log line.
            _logger.LogError(ex, "DENUE lookup failed for {SearchTerm} at {Lat},{Lng}", searchTerm, lat, lng);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Could not reach INEGI DENUE right now." });
        }
    }

    // GET /api/geomarketing/population-density?lat=19.04&lng=-98.20
    // Looks up the real 2020-census population of the INEGI AGEB (their smallest census
    // geography) containing the point — official demographic counterpart to business-density
    // above. Returns 404 for a point outside any imported AGEB, which today means outside
    // Puebla state (the only one imported so far) rather than a genuine error.
    // Its own rate-limit policy, not "geomarketing": this queries our own indexed Postgres data,
    // not INEGI's DENUE quota, so it has no reason to share (and halve) that budget.
    [HttpGet("population-density")]
    [EnableRateLimiting("population-density")]
    public async Task<ActionResult<PopulationDensityDto>> PopulationDensity(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken)
    {
        var latLngError = GeoValidation.ValidateLatLng(lat, lng);
        if (latLngError is not null) return BadRequest(new { message = latLngError });

        try
        {
            var result = await _populationDensityService.GetAsync(lat, lng, cancellationToken);
            return result is null
                ? NotFound(new { message = "No population data covers this location yet." })
                : Ok(result);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            // Unlike "not found" (a normal, expected result for anywhere outside the imported
            // states), this is the query itself failing — connection-pool exhaustion, a timeout,
            // or a malformed AGEB polygon from the import tripping PostGIS. The frontend already
            // treats any error here as "population unavailable" (see searchPopulationDensity's
            // .catch), so surfacing it as a 500 with a log line — not letting it bubble up as an
            // unhandled exception — matches business-density's own failure-handling shape.
            _logger.LogError(ex, "Population-density lookup failed at {Lat},{Lng}", lat, lng);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not look up population data right now." });
        }
    }
}
