namespace RealEstate.Api.Models.DTOs;

// The house/apartment counterpart to ViabilityAnalysisDto — instead of scoring business-type fit
// for a lot, this prices the subject listing against real nearby comparables and surfaces the
// same population/appreciation signals the commercial report uses. See ResidentialAnalysisService.
public class ResidentialAnalysisDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }

    public int ComparablesCount { get; set; }
    public decimal? SubjectPricePerSqm { get; set; }
    public decimal? AvgPricePerSqm { get; set; }
    public decimal? MedianPricePerSqm { get; set; }
    // 0-100: the share of nearby comparables priced at or below the subject's price/m² — a low
    // number means this listing is cheap relative to the market, a high number means it's pricey.
    // Null when there aren't enough comparables with a known area to rank against.
    public int? PercentileVsComparables { get; set; }
    public List<ListingDto> Comparables { get; set; } = new();

    public bool PopulationDataAvailable { get; set; }
    public int? PopulationInRadius { get; set; }
    public double? SocioeconomicScore { get; set; }

    // Same 0-100 "potencial de plusvalía (2 años)" score GeomarketingController's
    // appreciation-potential-at-point serves — null when no AGEB covers this point.
    public double? AppreciationScore { get; set; }
    public List<string> AppreciationExplanations { get; set; } = new();

    // Null (never a fabricated/zero estimate) when too few real nearby rentals of the same
    // property type exist to base a yield on — see ResidentialAnalysisService.MinRentListingsForYield.
    public RentalYieldEstimateDto? RentalYield { get; set; }

    // What a home buyer actually cares about nearby — schools, supermarkets, pharmacies,
    // hospitals, parks, gyms — sourced from DENUE the same way ViabilityAnalysisService's
    // TrafficGenerators are, but with categories chosen for "is this a good place to live"
    // instead of "will this location drive foot traffic to a business". Reuses TrafficGeneratorDto
    // since the shape (name/category/distance/lat/lng) is identical.
    public List<TrafficGeneratorDto> NearbyAmenities { get; set; } = new();
    public bool NearbyAmenitiesDataUnavailable { get; set; }
}

public class RentalYieldEstimateDto
{
    public int NearbyRentCount { get; set; }
    public decimal AvgMonthlyRent { get; set; }
    public decimal EstimatedAnnualYieldPct { get; set; }
}
