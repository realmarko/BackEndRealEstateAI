-- Loads SICT's "Datos viales (2024)" federal highway traffic-count stations into
-- traffic_count_stations (see TrafficCountStation.cs / AddTrafficCountStations migration).
-- Informational data only for the commercial viability report (TrafficCountStationProximity) —
-- see README.md for why this is never scored as a pass/fail criterion.
--
-- Run after the AddTrafficCountStations migration has been applied:
--   psql -U <user> -h <host> -d <db> -f import.sql
-- (adjust the \copy path below if running from somewhere other than this directory)
--
-- Source: SICT, "Datos viales" dataset, https://www.datos.gob.mx/dataset/datos_viales
-- See README.md in this folder for the column mapping and how to refresh this file.

BEGIN;

-- Column names are our own choice — COPY maps the CSV's columns to this table positionally (per
-- the file's actual column order), not by matching the CSV header's text, so there's no need to
-- replicate the source header's awkward names (e.g. its "k'" column) here.
CREATE TEMP TABLE staging_traffic (
  periodo text,
  estado text,
  carretera text,
  clave text,
  ruta text,
  punto_generador text,
  km numeric,
  te numeric,
  sc numeric,
  tdpa numeric,
  pct_m numeric,
  pct_a numeric,
  pct_b numeric,
  pct_c2 numeric,
  pct_c3 numeric,
  pct_t3s2 numeric,
  pct_t3s3 numeric,
  pct_t3s2r4 numeric,
  pct_otros numeric,
  pct_a1 numeric,
  pct_b1 numeric,
  pct_c numeric,
  k_factor numeric,
  d_factor numeric,
  latitud numeric,
  longitud numeric
);

\copy staging_traffic FROM 'datos_viales_2024.csv' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8')

-- Idempotent: reload the whole table from the CSV each run rather than accumulating duplicates —
-- same convention as ageb-socioeconomic/import.sql. This is a straight reference-data load (no
-- computed/derived columns), so there's nothing to recompute beyond the copy itself.
TRUNCATE traffic_count_stations;

INSERT INTO traffic_count_stations (route_name, route_key, state, tdpa, period, latitude, longitude)
SELECT
  carretera,
  ruta,
  estado,
  round(tdpa)::int,
  periodo,
  latitud::double precision,
  longitud::double precision
FROM staging_traffic
-- A handful of source rows have a blank/zero lat-lng (station location not captured) — those
-- would never match a listing's proximity search anyway and would only pollute the bbox
-- prefilter, so skip them at load time rather than filtering on every query later. ~1,188 of the
-- 10,326 rows (≈11.5%) also have a blank tdpa (not measured for this period) — tdpa is NOT NULL
-- on the table (every row we keep has to have a real figure to show), so those are skipped too
-- rather than silently inserted as 0, which would read as "zero traffic" instead of "not measured".
WHERE latitud IS NOT NULL AND longitud IS NOT NULL AND latitud <> 0 AND longitud <> 0
  AND tdpa IS NOT NULL;

COMMIT;
