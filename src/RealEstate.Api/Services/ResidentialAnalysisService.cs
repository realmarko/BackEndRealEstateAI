using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

public interface IResidentialAnalysisService
{
    Task<ResidentialAnalysisDto> AnalyzeAsync(Listing listing, CancellationToken cancellationToken = default);
}

// House/apartment counterpart to ViabilityAnalysisService — prices the listing against real
// nearby comparables (same ListingType/PropertyType, radius search) instead of scoring
// business-type fit, and reuses the exact same AGEB-lookup pattern GeomarketingController's
// appreciation-potential-at-point/population-density endpoints use (see their own comments on
// why this needs to be a service-level reuse, not an HTTP call back into this same API).
public class ResidentialAnalysisService : IResidentialAnalysisService
{
    // Same AreaSqFt (backend) -> sq m conversion the frontend adapter uses (listing-api.adapter.ts
    // SQM_PER_SQFT) — kept identical so a price/m² shown here matches what the frontend would
    // compute from the same listing's own areaSqm. Internal (not private): ResidentialAnalysisPdfService
    // reuses this exact constant for its own comparables table so the two never drift apart.
    internal const decimal SqmPerSqFt = 0.09290304m;

    // Mirrors ViabilityAnalysisService.NearbyFraccionamientoRadiusMeters's reasoning: a buyer
    // comparing houses cares about the immediate neighborhood, not the whole city. Widened once if
    // too few comparables turn up nearby (common outside dense urban cores) rather than returning
    // a near-empty comparison.
    private const double InitialRadiusMeters = 2000;
    private const double WidenedRadiusMeters = 5000;
    private const int MinComparablesBeforeWidening = 5;
    private const int MaxComparablesShown = 8;

    // A yield estimate from 1-2 nearby rentals is noise, not a real estimate — omit it entirely
    // rather than show a number built on almost nothing (see ResidentialAnalysisDto.RentalYield).
    private const int MinRentListingsForYield = 3;

    private readonly ApplicationDbContext _db;
    private readonly IPopulationDensityService _populationDensityService;
    private readonly IAppreciationPotentialService _appreciationPotentialService;
    private readonly ILogger<ResidentialAnalysisService> _logger;

    public ResidentialAnalysisService(
        ApplicationDbContext db,
        IPopulationDensityService populationDensityService,
        IAppreciationPotentialService appreciationPotentialService,
        ILogger<ResidentialAnalysisService> logger)
    {
        _db = db;
        _populationDensityService = populationDensityService;
        _appreciationPotentialService = appreciationPotentialService;
        _logger = logger;
    }

    public async Task<ResidentialAnalysisDto> AnalyzeAsync(Listing listing, CancellationToken cancellationToken = default)
    {
        var lat = listing.Latitude;
        var lng = listing.Longitude;

        var saleComparables = await FindComparablesAsync(listing, ListingType.Sale, cancellationToken);

        var pricesPerSqm = saleComparables
            .Where(n => n.Listing.AreaSqFt > 0)
            .Select(n => n.Listing.Price / (n.Listing.AreaSqFt * SqmPerSqFt))
            .OrderBy(p => p)
            .ToList();

        decimal? subjectPricePerSqm = listing.AreaSqFt > 0 ? listing.Price / (listing.AreaSqFt * SqmPerSqFt) : null;

        int? percentile = null;
        if (subjectPricePerSqm is { } subjectPrice && pricesPerSqm.Count > 0)
        {
            var atOrBelow = pricesPerSqm.Count(p => p <= subjectPrice);
            percentile = (int)Math.Round(100.0 * atOrBelow / pricesPerSqm.Count);
        }

        PopulationDensityDto? population = null;
        try
        {
            population = await _populationDensityService.GetAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Population-density lookup failed during residential analysis at {Lat},{Lng}", lat, lng);
        }

        var (appreciationScore, appreciationExplanations) = await ScoreAppreciationAsync(lat, lng, cancellationToken);
        var rentalYield = await EstimateRentalYieldAsync(listing, subjectPricePerSqm, cancellationToken);

        return new ResidentialAnalysisDto
        {
            Lat = lat,
            Lng = lng,
            ComparablesCount = saleComparables.Count,
            SubjectPricePerSqm = subjectPricePerSqm is { } sp ? Math.Round(sp, 0) : null,
            AvgPricePerSqm = pricesPerSqm.Count > 0 ? Math.Round(pricesPerSqm.Average(), 0) : null,
            MedianPricePerSqm = pricesPerSqm.Count > 0 ? Math.Round(MedianOf(pricesPerSqm), 0) : null,
            PercentileVsComparables = percentile,
            Comparables = saleComparables.Take(MaxComparablesShown).Select(n => n.Listing.ToDto(isAuthenticated: false)).ToList(),
            PopulationDataAvailable = population is not null,
            PopulationInRadius = population?.Population,
            SocioeconomicScore = population?.SocioeconomicScore,
            AppreciationScore = appreciationScore,
            AppreciationExplanations = appreciationExplanations,
            RentalYield = rentalYield
        };
    }

    // Same AGEB-containment lookup GeomarketingController.GetAppreciationPotentialAtPoint uses —
    // copied rather than called over HTTP, since this already runs server-side with its own
    // ApplicationDbContext in scope.
    private async Task<(double? Score, List<string> Explanations)> ScoreAppreciationAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        try
        {
            var point = GeoFactory.Instance.CreatePoint(new Coordinate(lng, lat));
            var ageb = await _db.AgebPopulations.AsNoTracking().FirstOrDefaultAsync(a => a.Boundary.Contains(point), cancellationToken);
            if (ageb is null) return (null, new List<string>());

            var scored = await _appreciationPotentialService.ScoreAgebAsync(ageb, cancellationToken);
            return (scored.Score, scored.Explanations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Appreciation-potential lookup failed during residential analysis at {Lat},{Lng}", lat, lng);
            return (null, new List<string>());
        }
    }

    private async Task<RentalYieldEstimateDto?> EstimateRentalYieldAsync(Listing listing, decimal? subjectPricePerSqm, CancellationToken cancellationToken)
    {
        if (listing.Price <= 0) return null;

        try
        {
            var rentComparables = await FindComparablesAsync(listing, ListingType.Rent, cancellationToken);
            if (rentComparables.Count < MinRentListingsForYield) return null;

            var avgMonthlyRent = rentComparables.Average(n => n.Listing.Price);
            var estimatedAnnualYieldPct = avgMonthlyRent * 12 / listing.Price * 100;

            return new RentalYieldEstimateDto
            {
                NearbyRentCount = rentComparables.Count,
                AvgMonthlyRent = Math.Round(avgMonthlyRent, 0),
                EstimatedAnnualYieldPct = Math.Round(estimatedAnnualYieldPct, 1)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rental-comparables lookup failed during residential analysis for listing {ListingId}", listing.Id);
            return null;
        }
    }

    private async Task<List<NearbyListing>> FindComparablesAsync(Listing listing, ListingType listingType, CancellationToken cancellationToken)
    {
        var found = await ListingProximity.FindNearbyAsync(
            _db, listing.Id, listing.Latitude, listing.Longitude, InitialRadiusMeters, listingType, listing.PropertyType, cancellationToken);

        if (found.Count < MinComparablesBeforeWidening)
        {
            found = await ListingProximity.FindNearbyAsync(
                _db, listing.Id, listing.Latitude, listing.Longitude, WidenedRadiusMeters, listingType, listing.PropertyType, cancellationToken);
        }

        return found;
    }

    private static decimal MedianOf(List<decimal> sorted)
    {
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2 : sorted[mid];
    }
}
