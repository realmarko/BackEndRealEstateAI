using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Extensions;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Services;

// Everything about the lot itself (not the surrounding area — population/competition come from
// DENUE/census lookups instead) that feeds the viability checks. All optional: this is reusable
// from a bare map click later (no listing attached yet), not just an existing Land listing.
public record ViabilityLotContext(
    double? FrontageMeters,
    double? LotSizeSqm,
    bool? IsCornerLot,
    bool? HasPavedStreet,
    bool? HasVehicleAccess,
    string? LandUseCategoryName,
    bool? HasPotableWater,
    bool? HasDrainage,
    bool? HasElectricity,
    TopographyType? Topography,
    bool? IsFloodRiskZone,
    bool? IsFreeOfLiens,
    bool? HasPropertyTaxDebt,
    bool? HasWaterDebt);

public interface IViabilityAnalysisService
{
    // Scores every BusinessTypeCriteria row against one point/lot, explaining each check — the
    // data behind the "Reporte de Viabilidad Comercial" lead magnet (see ListingsController's
    // viability-report endpoint) and the listing-detail preview section.
    Task<ViabilityAnalysisDto> AnalyzeAsync(double lat, double lng, ViabilityLotContext lot, CancellationToken cancellationToken = default);
}

public class ViabilityAnalysisService : IViabilityAnalysisService
{
    // A flood zone or unresolved debt/lien is a real-money, real-risk problem for ANY business
    // type, not just a point against one of many equally-weighted checks — capping the score
    // keeps a property with one serious blocker from still reading as "90% viable" just because
    // its lot dimensions happen to be generous.
    private const int HardBlockerScoreCap = 45;

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

    public async Task<ViabilityAnalysisDto> AnalyzeAsync(double lat, double lng, ViabilityLotContext lot, CancellationToken cancellationToken = default)
    {
        var criteria = await _db.BusinessTypeCriteria.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

        // Looked up once and reused by every business row below — the AGEB containing the point,
        // not a true per-business-radius buffer (that would need per-radius spatial queries);
        // documented as an approximation in each population check's Detail text. Only urban AGEBs
        // are imported today (see database/data-imports/ageb-boundaries/README.md) — a rural/
        // agricultural point genuinely has no AGEB covering it, which PopulationDataAvailable
        // reflects below rather than treating as a failure.
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
            SocioeconomicScore = population?.SocioeconomicScore,
            TrafficGenerators = await FindTrafficGeneratorsAsync(lat, lng, cancellationToken)
        };

        foreach (var criterion in criteria)
        {
            result.Businesses.Add(await ScoreBusinessAsync(criterion, lat, lng, lot, population, cancellationToken));
        }

        return result;
    }

    // Nearby anchors a business benefits from being close to, regardless of its own giro — a
    // school, a mall, a hospital all pull in foot/vehicle traffic. Sourced from DENUE (same
    // registry as the competitor checks) rather than Google Places, so this needs no extra
    // credential and shares DenueService's own 24h cache.
    private static readonly (string SearchTerm, string Category, int RadiusMeters)[] TrafficGeneratorSearches =
    {
        ("escuela", "Escuela", 1000),
        ("centro comercial", "Centro comercial", 1500),
        ("hospital", "Hospital", 1500)
    };
    // Per category, not a flat overall cap — otherwise an area with several nearby schools and
    // no mall/hospital in range would show 5 schools and nothing else, which reads as "we only
    // check for schools" rather than "nothing else was found nearby" (reported directly by the
    // user, 2026-10-18).
    private const int MaxTrafficGeneratorsPerCategory = 2;

    private async Task<List<TrafficGeneratorDto>> FindTrafficGeneratorsAsync(double lat, double lng, CancellationToken cancellationToken)
    {
        var searches = TrafficGeneratorSearches.Select(async s =>
        {
            try
            {
                var establishments = await _denueService.ListNearbyAsync(s.SearchTerm, lat, lng, s.RadiusMeters, cancellationToken);
                return establishments
                    .Select(e => new TrafficGeneratorDto
                    {
                        Name = e.Name,
                        DistanceMeters = (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, e.Lat, e.Lng)),
                        Category = s.Category
                    })
                    .OrderBy(g => g.DistanceMeters)
                    .Take(MaxTrafficGeneratorsPerCategory);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "DENUE traffic-generator lookup failed for {Category} at {Lat},{Lng}", s.Category, lat, lng);
                return Enumerable.Empty<TrafficGeneratorDto>();
            }
        });

        var results = await Task.WhenAll(searches);
        return results.SelectMany(r => r).OrderBy(g => g.DistanceMeters).ToList();
    }

    private async Task<BusinessViabilityDto> ScoreBusinessAsync(
        BusinessTypeCriteria criterion,
        double lat,
        double lng,
        ViabilityLotContext lot,
        PopulationDensityDto? population,
        CancellationToken cancellationToken)
    {
        var checks = new List<ViabilityCheckDto>();
        var hardBlockers = new List<string>();

        if (lot.FrontageMeters is not null)
        {
            var passed = lot.FrontageMeters.Value >= criterion.MinFrontageMeters;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Frente del terreno",
                Passed = passed,
                Detail = $"{lot.FrontageMeters:0.#} m de frente ({(passed ? "cumple" : "no cumple")} el mínimo de {criterion.MinFrontageMeters:0.#} m)."
            });
        }

        if (lot.LotSizeSqm is not null)
        {
            var passed = lot.LotSizeSqm.Value >= criterion.MinLotSizeSqm;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Superficie del terreno",
                Passed = passed,
                Detail = $"{lot.LotSizeSqm:0.#} m² ({(passed ? "cumple" : "no cumple")} el mínimo de {criterion.MinLotSizeSqm:0.#} m²)."
            });
        }

        if (criterion.PrefersCornerLot is not null && lot.IsCornerLot is not null)
        {
            var passed = criterion.PrefersCornerLot.Value == lot.IsCornerLot.Value;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Esquina",
                Passed = passed,
                Detail = criterion.PrefersCornerLot.Value
                    ? (lot.IsCornerLot.Value ? "Es esquina, como se prefiere para este giro." : "No es esquina; este giro suele preferir una ubicación en esquina.")
                    : (lot.IsCornerLot.Value ? "Es esquina; este giro suele preferir evitarla (más tráfico/ruido)." : "No es esquina, como se prefiere para este giro.")
            });
        }

        if (criterion.PrefersPrimaryRoadAccess && lot.HasPavedStreet is not null)
        {
            checks.Add(new ViabilityCheckDto
            {
                Label = "Acceso vehicular",
                Passed = lot.HasPavedStreet.Value,
                Detail = lot.HasPavedStreet.Value ? "Calle pavimentada con acceso vehicular." : "Sin calle pavimentada — este giro depende fuertemente del acceso vehicular."
            });
        }
        else if (lot.HasVehicleAccess is not null)
        {
            // Even when this business type doesn't specifically prefer a primary road, every
            // business needs SOME vehicle access — a lot that's landlocked is a problem
            // regardless of giro, so this still gets checked, just not weighted as heavily.
            checks.Add(new ViabilityCheckDto
            {
                Label = "Acceso vehicular",
                Passed = lot.HasVehicleAccess.Value,
                Detail = lot.HasVehicleAccess.Value ? "El predio tiene acceso vehicular." : "El predio no reporta acceso vehicular."
            });
        }

        // Uso de suelo: never "no data" when the listing has a LandUseCategory at all (unlike the
        // DENUE/population checks, this is the listing's own field, not an external lookup) —
        // whether a business CAN legally operate there is as fundamental as lot size.
        if (lot.LandUseCategoryName is not null)
        {
            var acceptable = criterion.AcceptableLandUseCategories
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var passed = acceptable.Contains(lot.LandUseCategoryName, StringComparer.OrdinalIgnoreCase);
            checks.Add(new ViabilityCheckDto
            {
                Label = "Uso de suelo",
                Passed = passed,
                Detail = passed
                    ? $"Uso de suelo \"{lot.LandUseCategoryName}\" permite este giro."
                    : $"Uso de suelo \"{lot.LandUseCategoryName}\" no es el típico para este giro (requiere trámite de cambio de uso de suelo)."
            });
        }

        // Utilities: any business needs these to operate, so this is one combined check (not
        // three) — a partial build-out (e.g. electricity but no drainage) still reads as
        // "missing services," not a technicality worth 3 separate lines.
        var utilityFlags = new[] { lot.HasPotableWater, lot.HasDrainage, lot.HasElectricity };
        if (utilityFlags.Any(f => f is not null))
        {
            var missing = new List<string>();
            if (lot.HasPotableWater == false) missing.Add("agua potable");
            if (lot.HasDrainage == false) missing.Add("drenaje");
            if (lot.HasElectricity == false) missing.Add("electricidad");
            var passed = missing.Count == 0;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Servicios",
                Passed = passed,
                Detail = passed ? "Cuenta con agua potable, drenaje y electricidad." : $"Falta: {string.Join(", ", missing)}."
            });
        }

        if (lot.Topography is not null)
        {
            var passed = lot.Topography.Value == TopographyType.Plana;
            checks.Add(new ViabilityCheckDto
            {
                Label = "Topografía",
                Passed = passed,
                Detail = passed ? "Terreno plano." : "Terreno inclinado — puede elevar el costo de construcción para cualquier giro."
            });
        }

        if (lot.IsFloodRiskZone is true)
        {
            checks.Add(new ViabilityCheckDto { Label = "Riesgo de inundación", Passed = false, Detail = "Zona de riesgo de inundación." });
            hardBlockers.Add("zona de riesgo de inundación");
        }
        else if (lot.IsFloodRiskZone is false)
        {
            checks.Add(new ViabilityCheckDto { Label = "Riesgo de inundación", Passed = true, Detail = "Sin riesgo de inundación reportado." });
        }

        var legalIssues = new List<string>();
        if (lot.IsFreeOfLiens == false) legalIssues.Add("gravámenes");
        if (lot.HasPropertyTaxDebt == true) legalIssues.Add("adeudo de predial");
        if (lot.HasWaterDebt == true) legalIssues.Add("adeudo de agua");
        if (legalIssues.Count > 0)
        {
            checks.Add(new ViabilityCheckDto { Label = "Situación legal", Passed = false, Detail = $"Pendiente: {string.Join(", ", legalIssues)}." });
            hardBlockers.Add($"situación legal pendiente ({string.Join(", ", legalIssues)})");
        }
        else if (lot.IsFreeOfLiens is not null || lot.HasPropertyTaxDebt is not null || lot.HasWaterDebt is not null)
        {
            checks.Add(new ViabilityCheckDto { Label = "Situación legal", Passed = true, Detail = "Sin gravámenes ni adeudos reportados." });
        }

        if (criterion.PopulationRadiusKm is not null)
        {
            if (population is null)
            {
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Población cercana",
                    Passed = null,
                    Detail = "Sin datos de población para esta ubicación (fuera de las zonas urbanas censadas que cubrimos hoy)."
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
                        : $"Zona censal con {population.Population:N0} habitantes estimados ({(passed ? "cumple el mínimo recomendado de" : "por debajo del mínimo recomendado de")} {criterion.MinPopulationInRadius.Value:N0})."
                });
            }
        }

        var nearbyCompetitors = new List<NearbyBusinessDto>();
        if (criterion.CompetitorExclusionRadiusMeters is not null)
        {
            try
            {
                var establishments = await _denueService.ListNearbyAsync(criterion.DenueSearchTerm, lat, lng, criterion.CompetitorExclusionRadiusMeters.Value, cancellationToken);
                var passed = establishments.Count == 0;
                checks.Add(new ViabilityCheckDto
                {
                    Label = "Competencia directa",
                    Passed = passed,
                    Detail = passed
                        ? $"Sin competidores de este giro registrados dentro de {criterion.CompetitorExclusionRadiusMeters.Value:N0} m."
                        : $"{establishments.Count} competidor(es) de este giro dentro de {criterion.CompetitorExclusionRadiusMeters.Value:N0} m."
                });
                nearbyCompetitors = establishments
                    .Select(e => new NearbyBusinessDto { Name = e.Name, DistanceMeters = (int)Math.Round(GeoFactory.DistanceMeters(lat, lng, e.Lat, e.Lng)) })
                    .OrderBy(e => e.DistanceMeters)
                    .Take(MaxNamedCompetitors)
                    .ToList();
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
        var rawScore = applicable.Count == 0 ? 0 : (int)Math.Round(100.0 * applicable.Count(c => c.Passed!.Value) / applicable.Count);
        var capped = hardBlockers.Count > 0 && rawScore > HardBlockerScoreCap;

        return new BusinessViabilityDto
        {
            Key = criterion.Key,
            DisplayName = criterion.DisplayName,
            Score = capped ? HardBlockerScoreCap : rawScore,
            ScoreCapReason = capped ? $"Limitado por: {string.Join("; ", hardBlockers)}." : null,
            Checks = checks,
            NearbyCompetitors = nearbyCompetitors
        };
    }

    // Shown to the nearest few named competitors, not the raw DENUE count alone — a visitor
    // deciding whether to pursue this lot can act on "Farmacias Guadalajara, 650 m" in a way they
    // can't act on "2 competitors".
    private const int MaxNamedCompetitors = 3;
}
