# Federal highway traffic-count (TDPA) import

Adds one row per SICT highway traffic-count station (`traffic_count_stations` table) — real
measured Tránsito Diario Promedio Anual (TDPA / AADT), not an estimate. Used by the commercial
viability report as **informational data only** (see `TrafficCountStationProximity` and its one
caller in `ViabilityAnalysisService`) — it is never scored as a pass/fail criterion, since we have
no sourced per-business-type TDPA threshold and inventing one would repeat the exact
unverified-threshold mistake worked through in `business_type_criteria` on 2026-11-04/05 (see
`project_business_criteria_research_backlog` memory).

## Source

SICT (Secretaría de Infraestructura, Comunicaciones y Transportes), "Datos viales" dataset on the
federal open-data portal: https://www.datos.gob.mx/dataset/datos_viales — license Creative Commons
Attribution 4.0. Downloaded file: "Datos viales (2024)" (`datos_viales_2024.csv`, ~2 MB, 10,326
stations), last updated by SICT 16 April 2026.

Direct download URL (`https://repodatos.atdt.gob.mx/api_update/secretaria_comunicaciones/datos_viales/datos_viales_2024.csv`)
returns HTTP 403 for `curl`/bot user agents (Akamai bot protection) — works fine from a normal
browser via the dataset page's "Descargar" link.

Coverage is the ~73,000 km federal highway network only — no municipal/urban streets. A listing on
a local city street will never have a nearby station; that's expected, not a bug.

## Columns kept

The source CSV has many more columns (vehicle-type composition percentages: motos, autos,
autobuses, camiones in several axle configurations, plus `km`/`te`/`sc`/`punto_generador`
metadata) — only these are imported, since nothing else is needed for the current use case
(showing "how busy is the nearest federal highway, really"):

| `traffic_count_stations` column | Source CSV column | Notes |
|---|---|---|
| `route_name` | `carretera` | e.g. "Acceso a Pabellon de Arteaga" |
| `route_key` | `ruta` | short route code, e.g. "MEX-045" |
| `state` | `estado` | Mexican state name, as published (not normalized to our `MexicanState` catalog) |
| `tdpa` | `tdpa` | Tránsito Diario Promedio Anual — the actual AADT figure |
| `period` | `periodo` | e.g. "Enero-diciembre 2024" |
| `latitude` / `longitude` | `latitud` / `longitud` | station location |

File encoding: plain UTF-8, no BOM, LF line endings — confirmed before import (unlike
`ageb-socioeconomic.csv`, this one has no CRLF gotcha to work around).

## Running the import

```bash
psql -U <user> -h <host> -d <db> -f import.sql
```

Run from this directory (the script's `\copy` uses a relative path to `datos_viales_2024.csv`).
Requires the `AddTrafficCountStations` migration to already be applied. Safe to re-run —
`import.sql` truncates and reloads the whole table from the CSV each time rather than
accumulating duplicate rows.

## Refreshing with a newer year

SICT republishes this dataset periodically. To refresh: download the current "Datos viales (…)"
CSV from the dataset page above (browser, not `curl` — see the 403 note), replace
`datos_viales_2024.csv` here (rename if the year changes, and update `import.sql`'s `\copy` path
to match), then re-run `import.sql`.
