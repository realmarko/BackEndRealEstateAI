using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    // Agent lives in RealEstateDbContext; Listing lives in ApplicationDbContext (it's really
    // just the Identity DbContext, reused for listings) — same split as AgentsController.
    // There's no EF-enforced FK between a listing's owner and an agent, so OwnerCompany is
    // attached after the fact by matching OwnerId to Agent.UserId (see AttachOwnerAgentDetailsAsync).
    private readonly RealEstateDbContext _agentsDb;
    private readonly IPhotoUploadService _photoUploadService;
    private readonly IEmailService _emailService;
    private readonly IOwnerAgentLookupService _ownerAgentLookup;
    private readonly IFactSheetBuilderService _factSheetBuilder;
    private readonly IViabilityAnalysisService _viabilityAnalysis;
    private readonly IViabilityReportPdfService _viabilityReportPdf;
    private readonly IStaticMapService _staticMap;
    private readonly ILogger<ListingsController> _logger;

    public ListingsController(
        ApplicationDbContext db,
        RealEstateDbContext agentsDb,
        IPhotoUploadService photoUploadService,
        IEmailService emailService,
        IOwnerAgentLookupService ownerAgentLookup,
        IFactSheetBuilderService factSheetBuilder,
        IViabilityAnalysisService viabilityAnalysis,
        IViabilityReportPdfService viabilityReportPdf,
        IStaticMapService staticMap,
        ILogger<ListingsController> logger)
    {
        _db = db;
        _agentsDb = agentsDb;
        _photoUploadService = photoUploadService;
        _emailService = emailService;
        _ownerAgentLookup = ownerAgentLookup;
        _factSheetBuilder = factSheetBuilder;
        _viabilityAnalysis = viabilityAnalysis;
        _viabilityReportPdf = viabilityReportPdf;
        _staticMap = staticMap;
        _logger = logger;
    }

    // Property types the "Reporte de Viabilidad Comercial" applies to — mirrors the frontend's
    // own LAND_OR_COMMERCIAL_PROPERTY_TYPES (listing.model.ts) exactly, so eligibility can't
    // drift between the page deciding whether to show the section and this endpoint deciding
    // whether to serve it.
    private static readonly HashSet<PropertyType> ViabilityEligiblePropertyTypes = new()
    {
        PropertyType.Land,
        PropertyType.Commercial,
        PropertyType.ResidentialLand,
        PropertyType.CommercialLand,
        PropertyType.IndustrialLand,
        PropertyType.Office,
        PropertyType.RetailSpace,
        PropertyType.Building,
        PropertyType.CommercialStorage,
        PropertyType.IndustrialStorage,
        PropertyType.IndustrialWarehouse
    };

    // GET /api/listings?city=Austin&listingType=Sale&minPrice=100000&...
    [HttpGet]
    public async Task<ActionResult<PagedResult<ListingDto>>> Search([FromQuery] ListingSearchQuery q)
    {
        var isAuthenticated = User.IsAuthenticated();
        var query = _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.Status != ListingStatus.Removed)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.City))
            query = query.Where(l => l.Address!.CityLower == q.City!.ToLower());
        if (q.ListingType.HasValue)
            query = query.Where(l => l.ListingType == q.ListingType);
        if (q.PropertyType.HasValue)
            query = query.Where(l => l.PropertyType == q.PropertyType);
        if (q.MinPrice.HasValue)
            query = query.Where(l => l.Price >= q.MinPrice);
        if (q.MaxPrice.HasValue)
            query = query.Where(l => l.Price <= q.MaxPrice);
        if (q.MinBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= q.MinBedrooms);
        if (q.MinBathrooms.HasValue)
            query = query.Where(l => l.Bathrooms >= q.MinBathrooms);

        if (q.BrokerageId.HasValue)
        {
            var agencyUserIds = await _agentsDb.Agents
                .Where(a => a.BrokerageId == q.BrokerageId && a.UserId != null)
                .Select(a => a.UserId!.Value)
                .ToListAsync();
            query = query.Where(l => agencyUserIds.Contains(l.OwnerId));
        }

        // Google Maps viewport bounding box filter (pan/zoom search)
        if (q.SwLat.HasValue && q.SwLng.HasValue && q.NeLat.HasValue && q.NeLng.HasValue)
        {
            // .NET's query-string double binder accepts NaN/Infinity/out-of-range values as
            // "valid" doubles — without this, a malformed corner silently returns zero results
            // (or every row, if the comparisons happen to invert) instead of a clear 400.
            if (!GeoValidation.IsValidLat(q.SwLat.Value))
                return BadRequest(new { message = "swLat must be a finite number between -90 and 90." });
            if (!GeoValidation.IsValidLat(q.NeLat.Value))
                return BadRequest(new { message = "neLat must be a finite number between -90 and 90." });
            if (!GeoValidation.IsValidLng(q.SwLng.Value))
                return BadRequest(new { message = "swLng must be a finite number between -180 and 180." });
            if (!GeoValidation.IsValidLng(q.NeLng.Value))
                return BadRequest(new { message = "neLng must be a finite number between -180 and 180." });
            if (q.SwLat.Value >= q.NeLat.Value)
                return BadRequest(new { message = "swLat must be less than neLat." });
            if (q.SwLng.Value >= q.NeLng.Value)
                return BadRequest(new { message = "swLng must be less than neLng." });

            // Uses the GIST-indexed Location column (see Listing.cs), not a direct lat/lng range
            // comparison — a plain B-tree on (Latitude, Longitude) can't efficiently satisfy a
            // true 2D range query, same reasoning as GeomarketingController's AGEB bbox search.
            var bbox = GeoFactory.CreateBoundingBox(q.SwLat.Value, q.SwLng.Value, q.NeLat.Value, q.NeLng.Value);
            query = query.Where(l => l.Location!.Intersects(bbox));
        }

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => l.ToDto(isAuthenticated))
            .ToListAsync();

        await AttachOwnerAgentDetailsAsync(items);

        return Ok(new PagedResult<ListingDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    // Comparative-market-analysis: an agent draws an area on the map (same freehand shape the
    // map's own area-search already uses) and gets back price-per-sqft/days-on-market stats for
    // active, priced listings inside it, split sale vs. rent, plus a capped comparables list.
    // Open to anonymous visitors like Search/GetById above — nothing here is agent-only data,
    // just a different view of the same public listings.
    [HttpPost("cma")]
    public async Task<ActionResult<CmaResultDto>> Cma(CmaRequestDto dto)
    {
        // Matches GeoFactory.CreatePolygon's own floor, checked here too so the error message
        // names the actual field instead of a generic unhandled-exception 500.
        if (dto.Points.Count < 3)
            return BadRequest(new { message = "At least 3 points are required to define an area." });
        // A pathologically large polygon (or a client bug sending way more vertices than any
        // real hand-drawn shape would) shouldn't get to spend unbounded CPU building/querying
        // it — nothing legitimate needs this many points for one map-drawn area.
        if (dto.Points.Count > 500)
            return BadRequest(new { message = "Area has too many points (max 500)." });

        foreach (var point in dto.Points)
        {
            var latLngError = GeoValidation.ValidateLatLng(point.Lat, point.Lng);
            if (latLngError is not null) return BadRequest(new { message = latLngError });
        }

        Geometry area;
        try
        {
            area = GeoFactory.CreatePolygon(dto.Points.Select(p => (p.Lat, p.Lng)).ToList());
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "The drawn area isn't a valid shape — try redrawing it." });
        }

        // CreatePolygon builds the ring without checking topology — a self-crossing path (the
        // visitor's drawn shape loops back over itself) constructs without throwing here, and
        // would only surface later as a Postgres/PostGIS error from Within() below instead of a
        // clean 400. A real mouse-drawn freehand shape very commonly has a minor self-intersection
        // like this (a wobble that briefly crosses an earlier segment) despite looking like a
        // perfectly fine shape on screen — the frontend's own point-in-polygon check tolerates
        // that, but GEOS's strict validity rules don't. Buffer(0) is the standard GEOS trick to
        // repair exactly this kind of minor self-intersection (it rebuilds the geometry from its
        // own boundary) before falling back to rejecting it outright — it can turn a self-crossing
        // Polygon into a MultiPolygon (e.g. a bowtie splits into two touching triangles), so this
        // stays typed as the general Geometry base (Within/Intersects both accept any Geometry)
        // rather than the specific Polygon GeoFactory.CreatePolygon returns.
        if (!area.IsValid)
        {
            area = area.Buffer(0);
            if (area.IsEmpty || !area.IsValid)
                return BadRequest(new { message = "The drawn area isn't a valid shape — try redrawing it." });
        }

        var query = _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.Status == ListingStatus.Active && !l.PriceOnRequest && l.Price > 0)
            .Where(l => l.Location!.Within(area));

        if (dto.PropertyType.HasValue)
            query = query.Where(l => l.PropertyType == dto.PropertyType);

        // Same GIST-indexed Location column as Search's bounding-box filter above — Within()
        // hits it the same way Intersects() does.
        var listings = await query.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync();

        var isAuthenticated = User.IsAuthenticated();
        // Capped well below the up-to-500 rows the stats are computed from — an agent comparing
        // prices needs the numbers, not 500 photo cards; the newest ones are the most useful
        // reference point (closest to current asking-price behavior in the area).
        const int maxComparables = 24;
        var comparables = listings.Take(maxComparables).Select(l => l.ToDto(isAuthenticated)).ToList();
        await AttachOwnerAgentDetailsAsync(comparables);

        return Ok(new CmaResultDto
        {
            Sale = BuildCmaStats(listings.Where(l => l.ListingType == ListingType.Sale)),
            Rent = BuildCmaStats(listings.Where(l => l.ListingType == ListingType.Rent)),
            Comparables = comparables
        });
    }

    private static CmaStatsDto BuildCmaStats(IEnumerable<Listing> matching)
    {
        var list = matching.ToList();
        if (list.Count == 0) return new CmaStatsDto { Count = 0 };

        var pricesPerSqFt = list
            .Where(l => l.AreaSqFt > 0)
            .Select(l => l.Price / l.AreaSqFt)
            .OrderBy(p => p)
            .ToList();

        var now = DateTime.UtcNow;

        return new CmaStatsDto
        {
            Count = list.Count,
            MinPrice = list.Min(l => l.Price),
            MaxPrice = list.Max(l => l.Price),
            AvgPricePerSqFt = pricesPerSqFt.Count > 0 ? pricesPerSqFt.Average() : null,
            MedianPricePerSqFt = pricesPerSqFt.Count > 0 ? MedianOf(pricesPerSqFt) : null,
            AvgDaysOnMarket = list.Average(l => (now - l.CreatedAt).TotalDays)
        };
    }

    // `sorted` must already be ascending — every caller sorts it for its own reasons just before
    // calling this, so re-sorting here would just repeat that work.
    private static decimal MedianOf(List<decimal> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }

    // Brokerage/company and the agent's own profile photo aren't Listing fields, so the "show
    // properties by brokerage" filter and the listing-detail contact card need them attached
    // here. Resolution/caching lives in OwnerAgentLookupService, shared with InquiriesController.
    private async Task AttachOwnerAgentDetailsAsync(List<ListingDto> items)
    {
        if (items.Count == 0) return;

        var resolved = await _ownerAgentLookup.GetManyAsync(items.Select(i => i.OwnerId));

        foreach (var item in items)
        {
            var info = resolved.GetValueOrDefault(item.OwnerId);
            item.OwnerCompany = info.Company;
            item.OwnerPhotoUrl = info.PhotoUrl;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListingDto>> GetById(Guid id)
    {
        var listing = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing is null) return NotFound();

        var dto = listing.ToDto(User.IsAuthenticated());
        await AttachOwnerAgentDetailsAsync([dto]);
        return Ok(dto);
    }

    // Fire-and-forget from the detail page on load (see listing-detail.component.ts) — the
    // frontend debounces repeat calls per browser via localStorage, so this isn't deduplicated
    // again here. A no-op 404 for a since-deleted listing is fine; the caller doesn't act on the
    // response either way.
    [EnableRateLimiting("view-count")]
    [HttpPost("{id:guid}/view")]
    public async Task<IActionResult> RecordView(Guid id)
    {
        var rows = await _db.Listings
            .Where(l => l.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ViewCount, l => l.ViewCount + 1));

        return rows == 0 ? NotFound() : NoContent();
    }

    // Anonymous, direct PDF download — the target of the "fact sheet" QR code shown on the
    // listing detail page: scanning it from a printed sign or flyer should hand over the fact
    // sheet immediately, with no login and no detour through the site itself. Rate-limited (see
    // Program.cs's "fact-sheet" policy) since, unlike a plain GET, this renders a PDF and
    // downloads several S3 photos per call — real cost an anonymous caller could otherwise
    // trigger on a loop against one known listing id.
    [EnableRateLimiting("fact-sheet")]
    [HttpGet("{id:guid}/fact-sheet.pdf")]
    public async Task<IActionResult> GetFactSheetPdf(Guid id)
    {
        var listing = await _db.Listings
            .Include(l => l.Address)
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();

        var pdfBytes = await _factSheetBuilder.BuildAsync(listing);
        return File(pdfBytes, "application/pdf", $"ficha-tecnica-{listing.Id}.pdf");
    }

    // GET /api/listings/5/viability-preview — the free, ungated part of the "Reporte de
    // Viabilidad Comercial" lead magnet: every business type's score + why, shown directly on
    // the listing-detail page. Only the PDF (ViabilityReport below) is gated behind a name/email.
    // Anonymous, its own rate limit (see Program.cs's "viability" policy) since this fans out to
    // several DENUE calls per request, not just one.
    [EnableRateLimiting("viability")]
    [HttpGet("{id:guid}/viability-preview")]
    public async Task<ActionResult<ViabilityAnalysisDto>> GetViabilityPreview(Guid id, CancellationToken cancellationToken)
    {
        var listing = await _db.Listings.AsNoTracking().Include(l => l.LandUseCategory).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (listing is null) return NotFound();
        if (!ViabilityEligiblePropertyTypes.Contains(listing.PropertyType)) return NotFound();

        var analysis = await _viabilityAnalysis.AnalyzeAsync(listing.Latitude, listing.Longitude, BuildLotContext(listing), cancellationToken);

        return Ok(analysis);
    }

    private static ViabilityLotContext BuildLotContext(Listing listing) => new(
        FrontageMeters: (double?)listing.FrontageWidthMeters,
        LotSizeSqm: (double?)listing.LotSizeSqm,
        IsCornerLot: listing.IsCornerLot,
        HasPavedStreet: listing.HasPavedStreet,
        HasVehicleAccess: listing.HasVehicleAccess,
        LandUseCategoryName: listing.LandUseCategory?.Name,
        HasPotableWater: listing.HasPotableWater,
        HasDrainage: listing.HasDrainage,
        HasElectricity: listing.HasElectricity,
        Topography: listing.Topography,
        IsFloodRiskZone: listing.IsFloodRiskZone,
        IsFreeOfLiens: listing.IsFreeOfLiens,
        HasPropertyTaxDebt: listing.HasPropertyTaxDebt,
        HasWaterDebt: listing.HasWaterDebt,
        PrimaryVialidadType: listing.PrimaryVialidadType);

    // Mirrors the interactive viability-report page's own marker color scheme
    // (viability-report-page.component.ts's CATEGORY_MARKER_COLORS) so the PDF's static map and
    // the live page read as the same product, not two different visualizations of the same data.
    private static readonly Dictionary<string, string> TrafficGeneratorMarkerColors = new()
    {
        ["Escuela"] = "blue",
        ["Centro comercial"] = "purple",
        ["Hospital"] = "red",
        ["Parada de autobús"] = "green",
        ["Central de autobuses"] = "green",
        ["Gasolinera"] = "yellow"
    };

    private static List<StaticMapMarker> BuildMapMarkers(ViabilityAnalysisDto analysis)
    {
        var markers = new List<StaticMapMarker>();
        foreach (var generator in analysis.TrafficGenerators)
        {
            var color = TrafficGeneratorMarkerColors.GetValueOrDefault(generator.Category, "gray");
            markers.Add(new StaticMapMarker(generator.Lat, generator.Lng, color));
        }
        foreach (var road in analysis.NearbyRoads)
        {
            markers.Add(new StaticMapMarker(road.Lat, road.Lng, "orange"));
        }
        return markers;
    }

    // POST /api/listings/5/viability-report — the gated half: captures the lead (same "answer a
    // few questions, get the PDF" shape as InquiriesController.Create, but anonymous — a lead
    // magnet has to work for a visitor with no account), emails the full report, AND hands the
    // same PDF back in the response body so the page can also offer an immediate download —
    // the lead is already captured either way, so there's no reason to make them wait on an
    // email just to see the document they were promised. Also leaves an Inquiry row (SenderId
    // null — there's no account) so the listing owner sees "someone requested a viability
    // report" in their own Inquiries inbox, same as any other lead.
    [EnableRateLimiting("fact-sheet")]
    [HttpPost("{id:guid}/viability-report")]
    public async Task<IActionResult> RequestViabilityReport(Guid id, ViabilityReportRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Name and email are required." });

        var listing = await _db.Listings
            .Include(l => l.Address)
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.LandUseCategory)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (listing is null) return NotFound();
        if (!ViabilityEligiblePropertyTypes.Contains(listing.PropertyType)) return NotFound();

        _db.Inquiries.Add(new Inquiry
        {
            ListingId = listing.Id,
            SenderId = null,
            SenderName = dto.Name,
            SenderEmail = dto.Email,
            SenderPhone = dto.Phone,
            Message = "Solicitó el Reporte de Viabilidad Comercial de este terreno."
        });
        await _db.SaveChangesAsync(cancellationToken);

        var analysis = await _viabilityAnalysis.AnalyzeAsync(listing.Latitude, listing.Longitude, BuildLotContext(listing), cancellationToken);
        var mapImage = await _staticMap.GetStaticMapAsync(listing.Latitude, listing.Longitude, BuildMapMarkers(analysis), cancellationToken);

        var listingDto = listing.ToDto(isAuthenticated: true);
        var pdfBytes = _viabilityReportPdf.Generate(listingDto, analysis, mapImage);

        // Best-effort, same reasoning as SendFactSheetPdfAsync: the lead is already captured
        // (the Inquiry above) and the PDF is returned to the caller regardless, so a failure
        // sending the email must never turn into an error response for this request.
        await SendViabilityReportPdfAsync(dto.Email, dto.Name, listing, pdfBytes);

        return File(pdfBytes, "application/pdf", $"viabilidad-comercial-{listing.Id}.pdf");
    }

    private async Task SendViabilityReportPdfAsync(string toEmail, string toName, Listing listing, byte[] pdfBytes)
    {
        try
        {
            var subject = $"Reporte de Viabilidad Comercial: {listing.Title}";
            var body =
                "Gracias por tu interés en este predio:\n\n" +
                $"{listing.Title}\n{listing.Address!.ToEmailLine()}\n\n" +
                "Adjuntamos el reporte de viabilidad comercial en PDF: qué tan apto es este terreno para distintos giros de negocio y por qué.";
            var fileName = $"viabilidad-comercial-{listing.Id}.pdf";
            await _emailService.SendAsync(toEmail, toName, subject, body, new EmailAttachment(pdfBytes, fileName, "application/pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send viability-report email to {Email} for listing {ListingId}", toEmail, listing.Id);
        }
    }

    // GET /api/listings/5/price-history — oldest first, so the frontend can render it as a
    // timeline (or compute deltas between consecutive entries) without re-sorting.
    [HttpGet("{id:guid}/price-history")]
    public async Task<ActionResult<List<ListingPriceHistoryDto>>> GetPriceHistory(Guid id)
    {
        var listingExists = await _db.Listings.AnyAsync(l => l.Id == id);
        if (!listingExists) return NotFound();

        var history = await _db.ListingPriceHistories
            .Where(h => h.ListingId == id)
            .OrderBy(h => h.RecordedAt)
            .Select(h => new ListingPriceHistoryDto
            {
                Price = h.Price,
                Currency = h.Currency,
                RecordedAt = h.RecordedAt
            })
            .ToListAsync();

        return Ok(history);
    }

    // Same city, listing type, and property type, ranked by how close the price is — a cheap
    // stand-in for a real recommendation engine, matching what the listing detail page's
    // "similar properties" section actually needs.
    [HttpGet("{id:guid}/similar")]
    public async Task<ActionResult<List<ListingDto>>> GetSimilar(Guid id)
    {
        var listing = await _db.Listings.Include(l => l.Address).FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();

        var similar = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.Id != id
                && l.Status == ListingStatus.Active
                && l.ListingType == listing.ListingType
                && l.PropertyType == listing.PropertyType
                && l.Address!.CityLower == listing.Address!.CityLower)
            .OrderBy(l => Math.Abs(l.Price - listing.Price))
            .Take(4)
            .Select(l => l.ToDto(User.IsAuthenticated()))
            .ToListAsync();

        return Ok(similar);
    }

    // Listings owned by the current user (for the "My Listings" dashboard)
    [Authorize(Roles = "Owner")]
    [HttpGet("mine")]
    public async Task<ActionResult<List<ListingDto>>> Mine()
    {
        var userId = User.GetUserId();
        var listings = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .Where(l => l.OwnerId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return Ok(listings.Select(l => l.ToDto(isAuthenticated: true)));
    }

    // Owners (including Agents, who are also Owners) may publish at most this many listings
    // per calendar month.
    private const int MaxListingsPerMonth = 15;

    // Shared by MyQuota() and Create() so the two never drift apart on how "this month" or
    // "used" is defined. Removed (soft-deleted) listings don't count — an owner who deletes a
    // mistake should get that slot back the same month, not have it stuck against them until
    // the month rolls over.
    private async Task<(int Used, DateTime StartOfMonth)> GetListingsUsedThisMonthAsync(Guid userId)
    {
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var used = await _db.Listings.CountAsync(l =>
            l.OwnerId == userId && l.CreatedAt >= startOfMonth && l.Status != ListingStatus.Removed);
        return (used, startOfMonth);
    }

    [Authorize(Roles = "Owner")]
    [HttpGet("mine/quota")]
    public async Task<ActionResult<ListingQuotaDto>> MyQuota()
    {
        var (used, startOfMonth) = await GetListingsUsedThisMonthAsync(User.GetUserId());

        return Ok(new ListingQuotaDto
        {
            Limit = MaxListingsPerMonth,
            Used = used,
            Remaining = Math.Max(0, MaxListingsPerMonth - used),
            ResetsAt = startOfMonth.AddMonths(1)
        });
    }

    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create([FromForm] ListingCreateDto dto)
    {
        if (!dto.PriceOnRequest && dto.Price <= 0)
        {
            return BadRequest(new { message = "Price must be greater than 0, or mark the listing as price-on-request." });
        }

        var userId = User.GetUserId();
        var (listingsThisMonth, _) = await GetListingsUsedThisMonthAsync(userId);
        if (listingsThisMonth >= MaxListingsPerMonth)
        {
            return BadRequest(new { message = $"You've reached the limit of {MaxListingsPerMonth} listings per month. Try again next month." });
        }

        // Generated up front so newly uploaded photos can be namespaced under this listing's
        // own id in S3 from the very first upload, rather than a temp/owner-scoped prefix.
        var listingId = Guid.NewGuid();

        var (imageUrls, uploadError) = await BuildImageUrlsAsync(dto.ExistingImageUrls, dto.Photos, $"listings/{listingId}");
        if (uploadError is not null) return uploadError;

        var listing = new Listing
        {
            Id = listingId,
            OwnerId = userId,
            Title = dto.Title,
            Description = dto.Description,
            ListingType = dto.ListingType,
            PropertyType = dto.PropertyType,
            Price = dto.Price,
            PriceOnRequest = dto.PriceOnRequest,
            Currency = dto.Currency,
            Address = new ListingAddress
            {
                Street = dto.Street,
                Colonia = dto.Colonia ?? string.Empty,
                City = dto.City,
                State = dto.State,
                ZipCode = dto.ZipCode ?? string.Empty,
                Country = dto.Country
            },
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaSqFt = dto.AreaSqFt,
            YearBuilt = dto.YearBuilt,
            ParkingSpaces = dto.ParkingSpaces,
            ParkingIsCovered = dto.ParkingIsCovered,
            Floors = dto.Floors,
            LotSizeSqm = dto.LotSizeSqm,
            GardenSizeSqm = dto.GardenSizeSqm,
            HasHeatingCooling = dto.HasHeatingCooling,
            HasRoofGarden = dto.HasRoofGarden,
            HoaFee = dto.HoaFee,
            VideoTourUrl = dto.VideoTourUrl,
            ConstructionCondition = dto.ConstructionCondition,
            FacadeOrientation = dto.FacadeOrientation,
            HasNaturalGas = dto.HasNaturalGas,
            HasLpGas = dto.HasLpGas,
            LandUseZoning = dto.LandUseZoning,
            LandUseCategoryId = dto.LandUseCategoryId,
            LandTenure = dto.LandTenure,
            HasEjidalCertificate = dto.HasEjidalCertificate,
            CosCoefficient = dto.CosCoefficient,
            CusCoefficient = dto.CusCoefficient,
            MaxHeightMeters = dto.MaxHeightMeters,
            IsFreeOfLiens = dto.IsFreeOfLiens,
            HasPropertyTaxDebt = dto.HasPropertyTaxDebt,
            HasWaterDebt = dto.HasWaterDebt,
            FrontageWidthMeters = dto.FrontageWidthMeters,
            FrontageDepthMeters = dto.FrontageDepthMeters,
            HasPotableWater = dto.HasPotableWater,
            HasDrainage = dto.HasDrainage,
            HasElectricity = dto.HasElectricity,
            HasThreePhaseElectricity = dto.HasThreePhaseElectricity,
            HasTelecomService = dto.HasTelecomService,
            HasVehicleAccess = dto.HasVehicleAccess,
            HasPavedStreet = dto.HasPavedStreet,
            HasNearbyUTurn = dto.HasNearbyUTurn,
            IsCornerLot = dto.IsCornerLot,
            StreetFrontageCount = dto.StreetFrontageCount,
            PrimaryVialidadType = dto.PrimaryVialidadType,
            LotShape = dto.LotShape,
            Topography = dto.Topography,
            IsFloodRiskZone = dto.IsFloodRiskZone,
            CadastralValue = dto.CadastralValue,
            Images = imageUrls.Select((url, idx) => new ListingImage
            {
                Url = url,
                IsPrimary = idx == 0,
                SortOrder = idx
            }).ToList()
        };

        listing.PriceHistory.Add(new ListingPriceHistory
        {
            ListingId = listing.Id,
            Price = listing.Price,
            Currency = listing.Currency,
            RecordedAt = listing.CreatedAt
        });

        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        var created = await _db.Listings
            .Include(l => l.Images).Include(l => l.Owner).Include(l => l.Address)
            .FirstAsync(l => l.Id == listing.Id);

        var createdDto = created.ToDto(isAuthenticated: true);
        // ListingService.create() splices this response straight into the same client-side
        // listings signal the company filter reads, so it needs OwnerCompany just like Search.
        await AttachOwnerAgentDetailsAsync([createdDto]);

        // Best-effort, same as AgentsController's review-notification email: the listing is
        // already saved, so a failure here must never turn into an error response for it. Unlike
        // that email (always exactly one recipient), this one fans out to every matching saved
        // search and is awaited inline before responding — fine at this app's current scale, but
        // a real bottleneck once the saved-searches table is large; a background job/queue would
        // be the right fix then, not something to build ahead of actual need here.
        await NotifySavedSearchesAsync(listing);

        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, createdDto);
    }

    // Emails every user whose saved search matches this brand-new listing (skipping the
    // listing's own owner, in case they also happen to have a matching saved search). Only
    // Create calls this — Update deliberately doesn't re-run it, so an edit that lowers a
    // listing's price into a saved search's range (a real, likely-common scenario, not just an
    // edge case) won't trigger an alert. Left out to keep the matching trigger simple; worth
    // revisiting if saved-search alerts turn out to matter more for price drops than for new listings.
    private async Task NotifySavedSearchesAsync(Listing listing)
    {
        var matches = await _db.SavedSearches
            .Include(s => s.User)
            .Where(s =>
                s.UserId != listing.OwnerId &&
                (s.ListingType == null || s.ListingType == listing.ListingType) &&
                (s.PropertyType == null || s.PropertyType == listing.PropertyType) &&
                (s.MinPrice == null || listing.Price >= s.MinPrice) &&
                (s.MaxPrice == null || listing.Price <= s.MaxPrice) &&
                (s.MinBedrooms == null || listing.Bedrooms >= s.MinBedrooms) &&
                (s.MinBathrooms == null || listing.Bathrooms >= s.MinBathrooms) &&
                (s.City == null || s.City.ToLower() == listing.Address!.City.ToLower()))
            // Caps the worst case for the synchronous per-request email loop below — without
            // this, a widely-matched search criteria (e.g. no filters at all) could block the
            // response for as long as it takes to attempt hundreds of SMTP sends one at a time.
            .Take(200)
            .ToListAsync();

        foreach (var match in matches)
        {
            if (match.User is null || string.IsNullOrWhiteSpace(match.User.Email)) continue;

            try
            {
                var subject = $"New listing matches your saved search \"{match.Name}\" on RealEstateApp";
                var body =
                    $"A new listing matches your saved search \"{match.Name}\":\n\n" +
                    $"{listing.Title}\n{listing.Address!.ToEmailLine()}\n{listing.Currency} {listing.Price}\n\n" +
                    "Log in to RealEstateApp to see it.";
                await _emailService.SendAsync(match.User.Email, match.User.FirstName, subject, body);
            }
            // NullReferenceException included alongside the SMTP/formatting failures this filter
            // was written for — same reasoning as InquiriesController.SendFactSheetLinkAsync:
            // this runs before Create()'s response is returned, so any exception here would
            // otherwise turn an already-saved listing into a failed request.
            catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException or NullReferenceException)
            {
                _logger.LogError(ex, "Failed to send saved-search alert email to user {UserId} for saved search {SavedSearchId}", match.UserId, match.Id);
            }
        }
    }

    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ListingDto>> Update(Guid id, [FromForm] ListingUpdateDto dto)
    {
        if (!dto.PriceOnRequest && dto.Price <= 0)
        {
            return BadRequest(new { message = "Price must be greater than 0, or mark the listing as price-on-request." });
        }

        var listing = await _db.Listings.Include(l => l.Images).Include(l => l.Address).FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != User.GetUserId()) return Forbid();

        // Captured before the fields below overwrite them, so RecordPriceChangeIfNeeded can
        // tell whether this edit actually changed the price/currency — an edit that leaves
        // them untouched must not add a new history row every time the listing is saved.
        var previousPrice = listing.Price;
        var previousCurrency = listing.Currency;

        // Upload before touching any existing rows, so a failed upload never destroys photos
        // that were already saved.
        var (imageUrls, uploadError) = await BuildImageUrlsAsync(dto.ExistingImageUrls, dto.Photos, $"listings/{id}");
        if (uploadError is not null) return uploadError;

        listing.Title = dto.Title;
        listing.Description = dto.Description;
        listing.ListingType = dto.ListingType;
        listing.PropertyType = dto.PropertyType;
        listing.Status = dto.Status;
        listing.Price = dto.Price;
        listing.PriceOnRequest = dto.PriceOnRequest;
        listing.Currency = dto.Currency;
        // Every listing gets an Address at creation and the FK is required — same trust level
        // as every other Address access in this controller (no ??= fallback: constructing a
        // fresh ListingAddress here if one were somehow missing would collide with the existing
        // row's PK on save, turning a clear NullReferenceException into a confusing
        // DbUpdateException instead of actually fixing anything).
        listing.Address!.Street = dto.Street;
        listing.Address.Colonia = dto.Colonia ?? string.Empty;
        listing.Address.City = dto.City;
        listing.Address.State = dto.State;
        listing.Address.ZipCode = dto.ZipCode ?? string.Empty;
        listing.Address.Country = dto.Country;
        listing.Latitude = dto.Latitude;
        listing.Longitude = dto.Longitude;
        listing.Bedrooms = dto.Bedrooms;
        listing.Bathrooms = dto.Bathrooms;
        listing.AreaSqFt = dto.AreaSqFt;
        listing.YearBuilt = dto.YearBuilt;
        listing.ParkingSpaces = dto.ParkingSpaces;
        listing.ParkingIsCovered = dto.ParkingIsCovered;
        listing.Floors = dto.Floors;
        listing.LotSizeSqm = dto.LotSizeSqm;
        listing.GardenSizeSqm = dto.GardenSizeSqm;
        listing.HasHeatingCooling = dto.HasHeatingCooling;
        listing.HasRoofGarden = dto.HasRoofGarden;
        listing.HoaFee = dto.HoaFee;
        listing.VideoTourUrl = dto.VideoTourUrl;
        listing.ConstructionCondition = dto.ConstructionCondition;
        listing.FacadeOrientation = dto.FacadeOrientation;
        listing.HasNaturalGas = dto.HasNaturalGas;
        listing.HasLpGas = dto.HasLpGas;
        listing.LandUseZoning = dto.LandUseZoning;
        listing.LandUseCategoryId = dto.LandUseCategoryId;
        listing.LandTenure = dto.LandTenure;
        listing.HasEjidalCertificate = dto.HasEjidalCertificate;
        listing.CosCoefficient = dto.CosCoefficient;
        listing.CusCoefficient = dto.CusCoefficient;
        listing.MaxHeightMeters = dto.MaxHeightMeters;
        listing.IsFreeOfLiens = dto.IsFreeOfLiens;
        listing.HasPropertyTaxDebt = dto.HasPropertyTaxDebt;
        listing.HasWaterDebt = dto.HasWaterDebt;
        listing.FrontageWidthMeters = dto.FrontageWidthMeters;
        listing.FrontageDepthMeters = dto.FrontageDepthMeters;
        listing.HasPotableWater = dto.HasPotableWater;
        listing.HasDrainage = dto.HasDrainage;
        listing.HasElectricity = dto.HasElectricity;
        listing.HasThreePhaseElectricity = dto.HasThreePhaseElectricity;
        listing.HasTelecomService = dto.HasTelecomService;
        listing.HasVehicleAccess = dto.HasVehicleAccess;
        listing.HasPavedStreet = dto.HasPavedStreet;
        listing.HasNearbyUTurn = dto.HasNearbyUTurn;
        listing.IsCornerLot = dto.IsCornerLot;
        listing.StreetFrontageCount = dto.StreetFrontageCount;
        listing.PrimaryVialidadType = dto.PrimaryVialidadType;
        listing.LotShape = dto.LotShape;
        listing.Topography = dto.Topography;
        listing.IsFloodRiskZone = dto.IsFloodRiskZone;
        listing.CadastralValue = dto.CadastralValue;
        listing.UpdatedAt = DateTime.UtcNow;
        RecordPriceChangeIfNeeded(listing, previousPrice, previousCurrency);

        _db.ListingImages.RemoveRange(listing.Images);
        listing.Images.Clear();
        await _db.SaveChangesAsync();

        foreach (var (url, idx) in imageUrls.Select((url, idx) => (url, idx)))
        {
            _db.ListingImages.Add(new ListingImage
            {
                ListingId = listing.Id,
                Url = url,
                IsPrimary = idx == 0,
                SortOrder = idx
            });
        }

        await _db.SaveChangesAsync();

        var updatedDto = listing.ToDto(isAuthenticated: true);
        // Same reason as Create: ListingService.update() splices this response into the
        // client-side listings signal, overwriting whatever OwnerCompany Search had attached.
        await AttachOwnerAgentDetailsAsync([updatedDto]);
        return Ok(updatedDto);
    }

    // Adds a price-history row only if this edit actually changed the price or currency —
    // called after both fields are assigned, so it reads the listing's own new values rather
    // than needing them passed in separately.
    private void RecordPriceChangeIfNeeded(Listing listing, decimal previousPrice, string previousCurrency)
    {
        if (listing.Price == previousPrice && listing.Currency == previousCurrency) return;

        _db.ListingPriceHistories.Add(new ListingPriceHistory
        {
            ListingId = listing.Id,
            Price = listing.Price,
            Currency = listing.Currency,
            RecordedAt = listing.UpdatedAt
        });
    }

    // Admin added alongside the existing owner path, not in place of it: an admin moderating
    // someone else's listing doesn't hold the Owner role, and Owner-role visitors must keep
    // being able to delete their own listings exactly as before.
    [Authorize(Roles = "Owner,Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (listing.OwnerId != User.GetUserId() && !User.IsInRole("Admin")) return Forbid();

        // Soft delete keeps the listing (and its inquiries/favorites) for history
        listing.Status = ListingStatus.Removed;
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Admin-only reassignment of a listing's ownership — e.g. correcting a listing entered
    // under the wrong account, or handing off an agent's book when they leave. The listing's
    // OwnerId must point at a real ApplicationUser (the same id every owner-only endpoint
    // authenticates as), so only an Agent with a linked UserId can receive one — a
    // directory-only Agent row (no account of its own) can't become a listing's owner.
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/transfer")]
    public async Task<ActionResult<ListingDto>> Transfer(Guid id, ListingTransferDto dto)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();

        var agent = await _agentsDb.Agents.FirstOrDefaultAsync(a => a.Id == dto.AgentId && !a.IsDeleted);
        if (agent is null) return NotFound(new { message = "Agent not found." });
        if (agent.UserId is null) return BadRequest(new { message = "This agent isn't linked to a user account yet." });

        var previousOwnerId = listing.OwnerId;
        listing.OwnerId = agent.UserId.Value;
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Both the old and new owner's cached agent-card info (company/photo shown on this
        // listing) are keyed by OwnerId — stale either one would show the wrong agent's details
        // until the cache's own expiry.
        _ownerAgentLookup.Evict(previousOwnerId);
        _ownerAgentLookup.Evict(listing.OwnerId);

        var updated = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .FirstAsync(l => l.Id == id);
        var updatedDto = updated.ToDto(isAuthenticated: true);
        await AttachOwnerAgentDetailsAsync([updatedDto]);
        return Ok(updatedDto);
    }

    // Admin-only, and deliberately a separate narrow endpoint rather than folded into Update
    // above: Update's authorization requires the caller to be the listing's exact OwnerId (no
    // Admin override), which an admin swapping in the real property owner's contact info —
    // typically for a listing they don't themselves own — would otherwise never pass. Keeping
    // this endpoint scoped to only these four fields means it can't be used to edit anything else
    // on a listing that isn't the caller's own.
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/owner-contact")]
    public async Task<ActionResult<ListingDto>> UpdateOwnerContact(Guid id, UpdateListingOwnerContactDto dto)
    {
        // Without this, an admin could hide the agent card and leave the public listing page with
        // no usable contact at all (a blank name/no button) — the one piece of information a
        // visitor actually needs is an email to write to.
        if (dto.HideAgentContact && string.IsNullOrWhiteSpace(dto.OwnerContactEmail))
        {
            return BadRequest(new { message = "An owner email is required to hide the agent's contact." });
        }

        var listing = await _db.Listings
            .Include(l => l.Images)
            .Include(l => l.Owner)
            .Include(l => l.Address)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (listing is null) return NotFound();

        listing.HideAgentContact = dto.HideAgentContact;
        listing.OwnerContactName = dto.OwnerContactName?.Trim();
        listing.OwnerContactPhone = dto.OwnerContactPhone?.Trim();
        listing.OwnerContactEmail = dto.OwnerContactEmail?.Trim();
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Gives the real owner a /owners/{id} directory profile right away — inactive (Owner.
        // UserId stays null) until someone registers an account with this same email (see
        // AuthController.Register, which claims a matching inactive row instead of leaving it
        // orphaned). OwnersController.GetListings/CountListingsAsync match this listing to that
        // profile by OwnerContactEmail, not Listing.OwnerId, so it works whether or not the row is
        // ever claimed. Only created, never overwritten — an existing (possibly already-claimed)
        // profile's own name/phone must never be clobbered by whatever an admin typed here.
        if (dto.HideAgentContact && !string.IsNullOrWhiteSpace(dto.OwnerContactEmail))
        {
            var normalizedEmail = dto.OwnerContactEmail.Trim();
            var hasOwnerProfile = await _agentsDb.Owners.AnyAsync(o => o.Email.ToLower() == normalizedEmail.ToLower());
            if (!hasOwnerProfile)
            {
                _agentsDb.Owners.Add(new Owner
                {
                    UserId = null,
                    Name = dto.OwnerContactName?.Trim() ?? string.Empty,
                    Email = normalizedEmail,
                    Phone = dto.OwnerContactPhone?.Trim() ?? string.Empty
                });
                try
                {
                    await _agentsDb.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Another request (a second listing getting the same owner's email set
                    // around the same time) won the AnyAsync-then-insert race and already created
                    // this row — the unique index on Email caught it. Already exactly the
                    // outcome this block was trying to reach, so this is a no-op, not a failure.
                }
            }
        }

        var updatedDto = listing.ToDto(isAuthenticated: true);
        await AttachOwnerAgentDetailsAsync([updatedDto]);
        return Ok(updatedDto);
    }

    // Sends the visitor's message straight to the real property owner's email (set via
    // UpdateOwnerContact above) — unlike the agent-contact flow (InquiriesController.Create), the
    // owner typically has no account/dashboard here to read inquiries from, so this has to be a
    // direct email. Same shape as AgentsController.Contact/OwnersController.Contact: anonymous,
    // rate-limited. Unlike those two, it ALSO persists an Inquiry row (flagged
    // IsOwnerContactNotification) purely so an Admin has a record of it in /mensajes — the owner
    // being emailed has no such view of their own.
    [EnableRateLimiting("contact")]
    [HttpPost("{id:guid}/contact-owner")]
    public async Task<IActionResult> ContactOwner(Guid id, ContactOwnerDto dto)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();

        if (!listing.HideAgentContact || string.IsNullOrWhiteSpace(listing.OwnerContactEmail))
            return NotFound();

        _db.Inquiries.Add(new Inquiry
        {
            ListingId = id,
            SenderId = User.TryGetUserId(),
            SenderName = dto.Name,
            SenderEmail = dto.Email,
            SenderPhone = dto.Phone,
            Message = dto.Message,
            IsOwnerContactNotification = true
        });
        await _db.SaveChangesAsync();

        var subject = $"New inquiry from {StripControlCharacters(dto.Name)} via RealEstateApp";
        var body =
            $"You have a new message from a potential client on RealEstateApp, about \"{listing.Title}\".\n\n" +
            $"Name: {dto.Name}\n" +
            $"Phone: {dto.Phone}\n" +
            $"Email: {dto.Email}\n\n" +
            $"Message:\n{dto.Message}";

        // Best-effort: the real owner usually has no account/dashboard here to look the listing
        // up on later, so attaching its fact sheet (specs, photos, address) gives them the full
        // picture right in the inbox. A failure here (a broken photo URL, a transient S3 hiccup)
        // must never block the contact email itself — the message still goes out, just without
        // the attachment.
        EmailAttachment? factSheetAttachment = null;
        try
        {
            var pdfBytes = await _factSheetBuilder.BuildAsync(listing);
            factSheetAttachment = new EmailAttachment(pdfBytes, $"ficha-tecnica-{listing.Id}.pdf", "application/pdf");
        }
        // Not a real failure: the visitor closed the tab or navigated away mid-request, which
        // aborts the in-flight photo downloads — logging that as an error would just be noise in
        // Sentry/the admin error log for something nobody did wrong.
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build the fact-sheet PDF for listing {ListingId}'s owner-contact email", id);
        }

        try
        {
            await _emailService.SendAsync(listing.OwnerContactEmail, listing.OwnerContactName ?? string.Empty, subject, body, factSheetAttachment);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or ArgumentException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to send contact email to listing {ListingId}'s owner contact", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Could not send the message right now. Please try again." });
        }

        return NoContent();
    }

    // Removes CR/LF (and other control characters) so a Subject built from user input can never
    // hit MailMessage's "not in the form required for a subject" ArgumentException — same
    // reasoning/implementation as AgentsController/OwnersController's own copy.
    private static string StripControlCharacters(string value) =>
        new(value.Where(c => !char.IsControl(c)).ToArray());

    // Combines already-hosted photo URLs (pasted links, or S3 URLs kept from a previous edit)
    // with newly uploaded files, uploading the latter to S3 and returning one ordered list —
    // existing photos first, then new ones in selection order. The first URL is the primary
    // photo. Returns an error result as-is if any file fails validation or the S3 upload fails.
    private const int MaxPhotosPerListing = 20;

    private async Task<(List<string> Urls, ActionResult? Error)> BuildImageUrlsAsync(
        List<string>? existingImageUrls, List<IFormFile>? photos, string keyPrefix)
    {
        var urls = new List<string>(existingImageUrls ?? new List<string>());
        var totalCount = urls.Count + (photos?.Count ?? 0);
        if (totalCount > MaxPhotosPerListing)
        {
            return (urls, BadRequest(new { message = $"A listing can have at most {MaxPhotosPerListing} photos." }));
        }

        if (photos is null || photos.Count == 0) return (urls, null);

        var result = await _photoUploadService.UploadManyAsync(photos, keyPrefix);
        if (result.ErrorKind is not null) return (urls, result.ErrorKind.Value.ToActionResult(this));

        urls.AddRange(result.Urls);
        return (urls, null);
    }
}
