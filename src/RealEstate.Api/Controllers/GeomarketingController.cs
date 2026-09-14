using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    private static readonly HashSet<string> AllowedSearchTerms = new(StringComparer.OrdinalIgnoreCase) { "farmacia" };

    private readonly IDenueService _denueService;
    private readonly ILogger<GeomarketingController> _logger;

    public GeomarketingController(IDenueService denueService, ILogger<GeomarketingController> logger)
    {
        _denueService = denueService;
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

        // .NET's query-string double binder accepts NaN/Infinity and any out-of-range number as
        // a "valid" double — without this check those would sail through to DenueService and
        // waste an external call (and a shared rate-limit slot) on a request INEGI can only reject.
        if (double.IsNaN(lat) || double.IsInfinity(lat) || lat is < -90 or > 90)
            return BadRequest(new { message = "lat must be a finite number between -90 and 90." });
        if (double.IsNaN(lng) || double.IsInfinity(lng) || lng is < -180 or > 180)
            return BadRequest(new { message = "lng must be a finite number between -180 and 180." });

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
}
