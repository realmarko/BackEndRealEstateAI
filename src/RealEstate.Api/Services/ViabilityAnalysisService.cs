using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Services;

public interface IViabilityAnalysisService
{
    // Scores every BusinessTypeCriteria row against one point/lot, explaining each check — the
    // data behind the "Reporte de Viabilidad Comercial" lead magnet (see ListingsController's
    // viability-report endpoint) and, later, the listing-detail preview section. Lot dimensions
    // are optional because the same analysis is meant to be reusable from a bare map click later
    // (no listing attached yet), not just from an existing Land listing's own fields.
    Task<ViabilityAnalysisDto> AnalyzeAsync(
        double lat,
        double lng,
        double? frontageMeters,
        double? lotSizeSqm,
        bool? isCornerLot,
        bool? hasPavedStreet,
        CancellationToken cancellationToken = default);
}

public class ViabilityAnalysisService : IViabilityAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly IDenueService _denueService;
    private readonly IPopulationDensityService _populationDensityService;
    private readonly ILogger<ViabilityAnalysisService> _logger;

    public ViabilityAnalysisService(
        ApplicationDbContext db,
        IDenueService denueService,
        IPopulationDensityService populationDensityService,
        ILogger<ViabilityAnalysisService> logger)
    {
        _db = db;
        _denueService = denueService;
        _populationDensityService = populationDensityService;
        _logger = logger;
    }

    public async Task<ViabilityAnalysisDto> AnalyzeAsync(
        double lat,
        double lng,
        double? frontageMeters,
        double? lotSizeSqm,
        bool? isCornerLot,
        bool? hasPavedStreet,
        CancellationToken cancellationToken = default)
    {
        var criteria = await _db.BusinessTypeCriteria.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

        // Looked up once and reused by every business row below — the AGEB containing the point,
        // not a true per-business-radius buffer (that would need per-radius spatial queries);
        // documented as an approximation in each population check's Detail text.
        PopulationDensityDto? population = null;
        try
        {
            population = await _populationDensityService.GetAsync(lat, lng, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Population-density lookup failed during viability analysis at {Lat},{Lng}", lat, lng);
        }

        var result = new ViabilityAnalysisDto
        {
            Lat = lat,
            Lng = lng,
            PopulationDataAvailable = population is not null,
            PopulationInRadius = population?.Population,
            SocioeconomicScore = population?.SocioeconomicScore
        };

        foreach (var criterion in criteria)
        {
            result.Businesses.Add(await ScoreBusinessAsync(criterion, lat, lng, frontageMeters, lotSizeSqm, isCornerLot, hasPavedStreet, population, cancellationToken));
        }

        return result;
    }

    private async Task<BusinessViabilityDto> ScoreBusinessAsync(
        Models.Entities.BusinessTypeCriteria criterion,
        double lat,
        double lng,
        double? frontageMeters,
        double? lotSizeSqm,
        bool? isCornerLot,
        bool? hasPavedStreet,
        PopulationDensityDto? population,
        CancellationToken cancellationToken)
    {
        var checks = new List<ViabilityCheckDto>();

        if (frontageMeters is not null)
        {
            var passed = frontageMeters.Value >= criterion.MinFrontageMeters;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Frente del terreno",
                Passed = passed,
                Detail = $"{frontageMeters:0.#} m de frente ({(passed ? "cumple" : "no cumple")} el mínimo de {criterion.MinFrontageMeters:0.#} m)."
            });
        }

        if (lotSizeSqm is not null)
        {
            var passed = lotSizeSqm.Value >= criterion.MinLotSizeSqm;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Superficie del terreno",
                Passed = passed,
                Detail = $"{lotSizeSqm:0.#} m² ({(passed ? "cumple" : "no cumple")} el mínimo de {criterion.MinLotSizeSqm:0.#} m²)."
            });
        }

        if (criterion.PrefersCornerLot is not null && isCornerLot is not null)
        {
            var passed = criterion.PrefersCornerLot.Value == isCornerLot.Value;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Esquina",
                Passed = passed,
                Detail = criterion.PrefersCornerLot.Value
                    ? (isCornerLot.Value ? "Es esquina, como se prefiere para este giro." : "No es esquina; este giro suele preferir una ubicación en esquina.")
                    : (isCornerLot.Value ? "Es esquina; este giro suele preferir evitarla (más tráfico/ruido)." : "No es esquina, como se prefiere para este giro.")
            });
        }

        if (criterion.PrefersPrimaryRoadAccess && hasPavedStreet is not null)
        {
            checks.Add(new ViabilityCheckDto
            {
                Label = "Acceso vehicular",
                Passed = hasPavedStreet.Value,
                Detail = hasPavedStreet.Value ? "Calle pavimentada con acceso vehicular." : "Sin calle pavimentada — este giro depende fuertemente del acceso vehicular."
            });
        }

        if (criterion.PopulationRadiusKm is not null)
        {
            if (population is null)
            {
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Población cercana",
                    Passed = null,
                    Detail = "Sin datos de población disponibles para esta ubicación todavía."
                });
            }
            else
            {
                var passed = criterion.MinPopulationInRadius is null || population.Population >= criterion.MinPopulationInRadius.Value;
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Población cercana",
                    Passed = passed,
                    Detail = criterion.MinPopulationInRadius is null
                        ? $"Zona censal con {population.Population:N0} habitantes estimados."
                        : $"Zona censal con {population.Population:N0} habitantes estimados ({(passed ? "cumple" : "por debajo de")} el mínimo recomendado de {criterion.MinPopulationInRadius.Value:N0})."
                });
            }
        }

        if (criterion.CompetitorExclusionRadiusMeters is not null)
        {
            try
            {
                var competitorCount = await _denueService.CountNearbyAsync(criterion.DenueSearchTerm, lat, lng, criterion.CompetitorExclusionRadiusMeters.Value, cancellationToken);
                var passed = competitorCount == 0;
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Competencia directa",
                    Passed = passed,
                    Detail = passed
                        ? $"Sin competidores de este giro registrados dentro de {criterion.CompetitorExclusionRadiusMeters.Value:N0} m."
                        : $"{competitorCount} competidor(es) de este giro dentro de {criterion.CompetitorExclusionRadiusMeters.Value:N0} m."
                });
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "DENUE competitor lookup failed for {Key} at {Lat},{Lng}", criterion.Key, lat, lng);
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Competencia directa",
                    Passed = null,
                    Detail = "No se pudo consultar el directorio de negocios en este momento."
                });
            }
        }

        var applicable = checks.Where(c => c.Passed is not null).ToList();
        var score = applicable.Count == 0 ? 0 : (int)Math.Round(100.0 * applicable.Count(c => c.Passed!.Value) / applicable.Count);

        return new BusinessViabilityDto
        {
            Key = criterion.Key,
            DisplayName = criterion.DisplayName,
            Score = score,
            Checks = checks
        };
    }
}
