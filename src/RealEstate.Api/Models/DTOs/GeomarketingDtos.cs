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

    // Socioeconomic proxy estimated from public INEGI Census indicators — NOT the commercial
    // AMAI NSE classification (see SocioeconomicLevel). Null for the AGEBs INEGI's own data
    // masking left with too few source indicators to estimate from.
    public double? SocioeconomicScore { get; set; }
    public string? EstimatedSocioeconomicLevel { get; set; }
}
