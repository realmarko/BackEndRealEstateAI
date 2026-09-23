using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using RealEstate.Api.Data;
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

    private static readonly GeoJsonWriter GeoJsonWriter = new();

    private readonly IDenueService _denueService;
    private readonly IPopulationDensityService _populationDensityService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<GeomarketingController> _logger;

    public GeomarketingController(
        IDenueService denueService,
        IPopulationDensityService populationDensityService,
        ApplicationDbContext db,
        ILogger<GeomarketingController> logger)
    {
        _denueService = denueService;
        _populationDensityService = populationDensityService;
        _db = db;
        _logger = logger;
    }

    private static JsonElement ToGeoJsonElement(Geometry geometry)
    {
        using var doc = JsonDocument.Parse(GeoJsonWriter.Write(geometry));
        return doc.RootElement.Clone();
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

    // GET /api/geomarketing/municipalities — small, static-ish list (217 rows for Puebla today),
    // meant for a client-side dropdown/autocomplete — the frontend fetches this once and caches
    // it, so no pagination.
    // Own rate-limit policy, same reasoning as population-density above: this is our own indexed
    // Postgres data, not INEGI's DENUE quota, so it shouldn't share (and shrink) that budget.
    [HttpGet("municipalities")]
    [EnableRateLimiting("population-density")]
    public async Task<ActionResult<List<MunicipalityListItemDto>>> ListMunicipalities(CancellationToken cancellationToken)
    {
        try
        {
            var municipalities = await _db.MunicipalBoundaries
                .AsNoTracking()
                .OrderBy(m => m.Name)
                .Select(m => new MunicipalityListItemDto { Cvegeo = m.Cvegeo, Name = m.Name, StateName = m.StateName })
                .ToListAsync(cancellationToken);

            return Ok(municipalities);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to list municipalities");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not load municipalities right now." });
        }
    }

    // GET /api/geomarketing/states — all 32 Mexican states, for the listing form's State field.
    // Static reference data (seeded once, see the AddMexicanStates migration), unlike
    // MunicipalBoundaries which only has real municipio/city data imported for Puebla so far —
    // this lets the form offer every state even where there's no city catalog behind it yet.
    [HttpGet("states")]
    [EnableRateLimiting("population-density")]
    public async Task<ActionResult<List<StateListItemDto>>> ListStates(CancellationToken cancellationToken)
    {
        try
        {
            var states = await _db.MexicanStates
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new StateListItemDto { Code = s.Code, Name = s.Name })
                .ToListAsync(cancellationToken);

            return Ok(states);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to list states");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not load states right now." });
        }
    }

    // GET /api/geomarketing/municipalities/{cvegeo}/boundary — the polygon a visitor selected
    // from ListMunicipalities, as GeoJSON, for the frontend to draw as a google.maps.Polygon and
    // filter listings against client-side (see map-view.component.ts).
    [HttpGet("municipalities/{cvegeo}/boundary")]
    [EnableRateLimiting("population-density")]
    public async Task<ActionResult<MunicipalityBoundaryDto>> GetMunicipalityBoundary(string cvegeo, CancellationToken cancellationToken)
    {
        try
        {
            var municipality = await _db.MunicipalBoundaries.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Cvegeo == cvegeo, cancellationToken);
            if (municipality is null) return NotFound(new { message = "No municipality with this cvegeo." });

            return Ok(new MunicipalityBoundaryDto
            {
                Cvegeo = municipality.Cvegeo,
                Name = municipality.Name,
                Boundary = ToGeoJsonElement(municipality.Boundary)
            });
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to load boundary for municipality {Cvegeo}", cvegeo);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not load this municipality's boundary right now." });
        }
    }

    // Bounds the /agebs query's result size — roughly the extent of the whole state, comfortably
    // above any real map viewport, but not so large that a caller can force a near-total scan of
    // Puebla's ~2,503 AGEBs (each with full boundary geometry) in a single request.
    private const double MaxAgebBoundsSpanDegrees = 2.0;

    // GET /api/geomarketing/agebs?swLat=&swLng=&neLat=&neLng= — AGEBs intersecting the given
    // viewport, for the map's optional "zonas censales" layer (see map-view.component.ts).
    // Scoped to the current viewport rather than all AGEBs at once — the state has 2,503 of them,
    // most never visible at once.
    // Own rate-limit policy, same reasoning as population-density above: this is our own indexed
    // Postgres data, not INEGI's DENUE quota — and unlike that endpoint, the frontend refetches
    // this on every pan/zoom while the layer is on, so sharing "geomarketing"'s budget would let
    // a visitor panning around the map starve their own (or a NAT-neighbor's) DENUE lookups.
    [HttpGet("agebs")]
    [EnableRateLimiting("population-density")]
    public async Task<ActionResult<List<AgebBoundaryDto>>> ListAgebsInBounds(
        [FromQuery] double swLat,
        [FromQuery] double swLng,
        [FromQuery] double neLat,
        [FromQuery] double neLng,
        CancellationToken cancellationToken)
    {
        if (!GeoValidation.IsValidLat(swLat)) return BadRequest(new { message = "swLat must be a finite number between -90 and 90." });
        if (!GeoValidation.IsValidLat(neLat)) return BadRequest(new { message = "neLat must be a finite number between -90 and 90." });
        if (!GeoValidation.IsValidLng(swLng)) return BadRequest(new { message = "swLng must be a finite number between -180 and 180." });
        if (!GeoValidation.IsValidLng(neLng)) return BadRequest(new { message = "neLng must be a finite number between -180 and 180." });
        if (swLat >= neLat) return BadRequest(new { message = "swLat must be less than neLat." });
        if (swLng >= neLng) return BadRequest(new { message = "swLng must be less than neLng." });
        if (neLat - swLat > MaxAgebBoundsSpanDegrees || neLng - swLng > MaxAgebBoundsSpanDegrees)
            return BadRequest(new { message = $"Bounding box must not exceed {MaxAgebBoundsSpanDegrees} degrees in either dimension." });

        var bbox = GeoFactory.Instance.CreatePolygon(new[]
        {
            new Coordinate(swLng, swLat),
            new Coordinate(neLng, swLat),
            new Coordinate(neLng, neLat),
            new Coordinate(swLng, neLat),
            new Coordinate(swLng, swLat)
        });

        try
        {
            var agebs = await _db.AgebPopulations.AsNoTracking()
                .Where(a => a.Boundary.Intersects(bbox))
                .ToListAsync(cancellationToken);

            var result = agebs.Select(a => new AgebBoundaryDto
            {
                Cvegeo = a.Cvegeo,
                Boundary = ToGeoJsonElement(a.Boundary),
                EstimatedSocioeconomicLevel = a.EstimatedSocioeconomicLevel?.ToString()
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to list AGEBs in bounds {SwLat},{SwLng} - {NeLat},{NeLng}", swLat, swLng, neLat, neLng);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Could not load census zones right now." });
        }
    }
}
