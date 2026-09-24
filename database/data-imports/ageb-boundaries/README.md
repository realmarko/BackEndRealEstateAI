# AGEB boundaries + population import

Populates the base rows of `ageb_populations` — `cvegeo`, `population`, `area_sq_km`,
`boundary` — for every urban AGEB (Área Geoestadística Básica, INEGI's smallest census
geography, roughly a few city blocks) in Puebla. This is the layer `../ageb-socioeconomic/`'s
overlay depends on: that script only `UPDATE`s rows that already exist here, so **this import
must run first**, and its own migration (`AddAgebPopulation`) must already be applied.

Distinct from `../municipal-boundaries/` (one polygon per *municipality*, ~217 for Puebla) —
this is the much finer AGEB level, ~2,496 polygons for Puebla alone.

## Sources

Two separate INEGI products, joined by `cvegeo`:

1. **Geometry** — Marco Geoestadístico, state of Puebla (21):
   https://www.inegi.org.mx/app/biblioteca/ficha.html?upc=889463807469
   (direct file: `21_puebla.zip`, ~152 MB, published 2021-01-21 — same ZIP
   `../municipal-boundaries/` uses, just a different shapefile inside it: `21a.shp`
   — "áreas geoestadísticas básicas urbanas" per the bundle's own `catalogos/contenido.txt`,
   not `21mun.shp`). Ships `CVEGEO` directly in the `.dbf` attributes (state+municipality+
   locality+AGEB, 13 chars) — no need to concatenate it from parts like the municipal import does.

2. **Population** — Censo de Población y Vivienda 2020, "Principales resultados por AGEB y
   manzana urbana", state of Puebla (21):
   https://www.inegi.org.mx/app/scitel/Default?ev=10
   (direct file: `RESAGEBURB_21CSV20.csv`, ~45 MB uncompressed, inside `resageburb_21csv20.zip`
   ~11.3 MB — same source file `../ageb-socioeconomic/` derives its indicators from). Only the
   `POBTOT` column is used here; filtered to `NOM_LOC == "Total AGEB urbana"` rows, same as the
   socioeconomic import, and `cvegeo` built the same way (`ENTIDAD+MUN+LOC+AGEB`) — which happens
   to match `21a.shp`'s own `CVEGEO` field exactly, confirmed for all 2,496 Puebla AGEBs (0 gaps
   either direction) when this was last run.

Both shapefiles use the same projection (Lambert Conformal Conic, ITRF2008/GRS80) as
`../municipal-boundaries/` — `convert.js` reuses the identical reprojection parameters.

`area_sq_km` is **not** computed in JS — the generated `import.sql` computes it in Postgres via
`ST_Area(boundary::geography) / 1000000.0` (PostGIS's geodesic area) once the geometry is already
in the database, rather than hand-rolling spherical-polygon math for a one-off script.

## Regenerating `import.sql` from a fresh download

The raw shapefile and census CSV aren't checked in (too large) — only the derived `import.sql`
(WKT already reprojected, human-readable/diffable) is. To regenerate:

1. Download `21_puebla.zip` from the Marco Geoestadístico URL above; extract
   `conjunto_de_datos/21a.cpg`, `.dbf`, `.prj`, `.shp`, `.shx` into this directory.
2. Download `resageburb_21csv20.zip` from the census URL above; extract
   `RESAGEBURB_21CSV20.csv` into this directory too.
3. `npm install` (installs `shapefile` and `proj4`, same as `../municipal-boundaries/` — no
   GDAL/native deps needed).
4. `npm run convert` — reads `21a.shp` + `RESAGEBURB_21CSV20.csv`, writes `import.sql`. Logs how
   many of the shapefile's polygons found a matching census population row; any that don't are
   **skipped, not defaulted to 0** — writing a fabricated population value for an AGEB with no
   real census match would be worse than leaving it absent from the table (the population-density
   endpoint already handles "no data for this location" as its own case).

## Running the import

```bash
psql -U <user> -h <host> -d <db> -f import.sql
```

Requires the `AddAgebPopulation` migration to already be applied. Safe to re-run — upserts on
`cvegeo` via `ON CONFLICT DO UPDATE` rather than accumulating duplicates. **Run before**
`../ageb-socioeconomic/import.sql`, which updates rows this script creates.

(If `psql` isn't available in your environment, any client that can execute a plain multi-statement
`.sql` file over a Postgres connection works too — this file uses no `psql`-only meta-commands
like `\copy`, unlike `../ageb-socioeconomic/import.sql`.)

## Extending to other states

Puebla is state code `21`. Adding another state means downloading its own `<code>_<name>.zip`
(geometry) and `resageburb_<code>csv20.zip` (population), extracting that state's `<code>a.shp`
and CSV, and re-running `convert.js` against them — no hardcoded state name to update this time
(everything keys off `cvegeo`, which already encodes the state).
