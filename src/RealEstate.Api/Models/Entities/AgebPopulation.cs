using NetTopologySuite.Geometries;

namespace RealEstate.Api.Models.Entities;

// One row per AGEB (Área Geoestadística Básica) — INEGI's smallest census geography unit,
// roughly a few city blocks in urban areas. Imported once from INEGI's Marco Geoestadístico
// (boundary) and Censo de Población y Vivienda 2020 (population), joined on Cvegeo. Static
// reference data: refreshed only when a new census/cartography edition is imported, not on
// every request — unlike DenueService's live proxy to INEGI's business-registry API.
public class AgebPopulation
{
    // The 13-digit code INEGI uses to uniquely identify an AGEB nationwide:
    // state(2) + municipality(3) + locality(4) + AGEB(4). No reader needs the individual parts
    // today — substring Cvegeo if a future feature (e.g. "filter by municipality") needs them.
    public string Cvegeo { get; set; } = null!;

    public int Population { get; set; }

    // Precomputed at import time so density queries don't recompute ST_Area per request.
    public double AreaSqKm { get; set; }

    // SRID 4326 (WGS84), matching the lat/lng the map and Google Places already use.
    public Geometry Boundary { get; set; } = null!;

    // The census/cartography edition this row came from (e.g. 2020) — a property of the
    // imported data, not a fact about the UI, so the frontend's population-density label is
    // built from this instead of a hardcoded year that would go silently stale the moment a
    // future import uses a different vintage.
    public int CensusYear { get; set; }
}
