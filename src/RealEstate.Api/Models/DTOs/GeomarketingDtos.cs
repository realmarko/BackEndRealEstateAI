namespace RealEstate.Api.Models.DTOs;

public class BusinessDensityDto
{
    public int Count { get; set; }
}

public class PopulationDensityDto
{
    public int Population { get; set; }
    public double AreaSqKm { get; set; }
    public double DensityPerSqKm { get; set; }
    public int CensusYear { get; set; }
}
