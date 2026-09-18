using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IPopulationDensityService
{
    // Finds the AGEB (INEGI's smallest census geography) containing (lat, lng) and returns its
    // real 2020-census population and density. Returns null for a point outside any imported
    // AGEB — today that means anywhere outside the state(s) whose Marco Geoestadístico/Censo
    // data has been imported (Puebla only, initially), not necessarily an error.
    Task<PopulationDensityDto?> GetAsync(double lat, double lng, CancellationToken cancellationToken = default);
}

public class PopulationDensityService : IPopulationDensityService
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    private readonly ApplicationDbContext _db;

    public PopulationDensityService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PopulationDensityDto?> GetAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        // NetTopologySuite Point takes (x, y) i.e. (longitude, latitude) — swapping these would
        // silently look up the wrong hemisphere's worth of AGEBs for most of Mexico.
        var point = GeometryFactory.CreatePoint(new Coordinate(lng, lat));

        var ageb = await _db.AgebPopulations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Boundary.Contains(point), cancellationToken);

        if (ageb is null) return null;

        return new PopulationDensityDto
        {
            Population = ageb.Population,
            AreaSqKm = ageb.AreaSqKm,
            DensityPerSqKm = ageb.AreaSqKm > 0 ? ageb.Population / ageb.AreaSqKm : 0,
            CensusYear = ageb.CensusYear,
            SocioeconomicScore = ageb.SocioeconomicScore,
            EstimatedSocioeconomicLevel = ageb.EstimatedSocioeconomicLevel?.ToString()
        };
    }
}
