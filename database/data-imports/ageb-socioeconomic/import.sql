-- Imports the AGEB-level socioeconomic proxy indicators from ageb_socioeconomic.csv into the
-- ageb_populations table (see AgebPopulation.cs / AddAgebSocioeconomicData migration), then
-- derives a percentile-based composite score and quintile level per AGEB.
--
-- Run after the AddAgebSocioeconomicData migration has been applied:
--   psql -U <user> -h <host> -d <db> -f import.sql
-- (adjust the \copy path below if running from somewhere other than this directory)
--
-- Source: INEGI, Censo de Poblacion y Vivienda 2020, "Principales resultados por AGEB y
-- manzana urbana", state of Puebla (21) — https://www.inegi.org.mx/app/scitel/Default?ev=10
-- See README.md in this folder for how ageb_socioeconomic.csv was derived from INEGI's raw file.

BEGIN;

CREATE TEMP TABLE staging_socio (
  cvegeo varchar(13),
  avg_schooling_years numeric,
  pct_homes_internet numeric,
  pct_homes_car numeric,
  pct_homes_computer numeric,
  avg_occupants_per_home numeric
);

\copy staging_socio FROM 'ageb_socioeconomic.csv' WITH (FORMAT csv, HEADER true, NULL '')

-- 0) Reset every column this script owns before reapplying from the CSV. Without this, an AGEB
-- that drops out of a re-import (removed from the source file, or its data masked down below the
-- 3-of-5 threshold below) would keep serving its previous run's values forever — the UPDATEs
-- below only touch cvegeos present in staging_socio/leveled, they don't clear anyone absent from
-- them, so a stale row would silently diverge from "what the current CSV says" without either
-- table ever recording that it happened.
UPDATE ageb_populations
SET
  avg_schooling_years = NULL,
  pct_homes_with_internet = NULL,
  pct_homes_with_car = NULL,
  pct_homes_with_computer = NULL,
  avg_occupants_per_home = NULL,
  socioeconomic_score = NULL,
  estimated_socioeconomic_level = NULL;

-- 1) Raw indicators: copied straight across, one row per AGEB already in ageb_populations.
UPDATE ageb_populations a
SET
  avg_schooling_years = s.avg_schooling_years,
  pct_homes_with_internet = s.pct_homes_internet,
  pct_homes_with_car = s.pct_homes_car,
  pct_homes_with_computer = s.pct_homes_computer,
  avg_occupants_per_home = s.avg_occupants_per_home
FROM staging_socio s
WHERE a.cvegeo = s.cvegeo;

-- 2) Percentile-rank each available metric independently (a NULL/masked cell in one AGEB
-- doesn't skew the ranking of the metrics every other AGEB does have), inverting
-- avg_occupants_per_home since fewer people per home is the higher-NSE direction.
CREATE TEMP TABLE metric_ranks AS
SELECT cvegeo, percent_rank() OVER (ORDER BY avg_schooling_years) AS rnk
FROM staging_socio WHERE avg_schooling_years IS NOT NULL
UNION ALL
SELECT cvegeo, percent_rank() OVER (ORDER BY pct_homes_internet)
FROM staging_socio WHERE pct_homes_internet IS NOT NULL
UNION ALL
SELECT cvegeo, percent_rank() OVER (ORDER BY pct_homes_car)
FROM staging_socio WHERE pct_homes_car IS NOT NULL
UNION ALL
SELECT cvegeo, percent_rank() OVER (ORDER BY pct_homes_computer)
FROM staging_socio WHERE pct_homes_computer IS NOT NULL
UNION ALL
SELECT cvegeo, percent_rank() OVER (ORDER BY avg_occupants_per_home DESC)
FROM staging_socio WHERE avg_occupants_per_home IS NOT NULL;

-- 3) Composite = average of the ranks an AGEB actually has. Below 3 of 5 metrics, the average
-- is too thin to stand behind as an estimate, so the AGEB is left unscored (NULL) rather than
-- given a confident-looking label built from one or two data points.
CREATE TEMP TABLE composite AS
SELECT cvegeo, AVG(rnk) * 100 AS score
FROM metric_ranks
GROUP BY cvegeo
HAVING COUNT(*) >= 3;

-- 4) Quintile bucket by construction (ntile(5) over the composite itself, not fixed 20-point
-- bands over the average-of-ranks, which clusters toward the middle) — each level ends up with
-- roughly a fifth of the scored AGEBs, which is what a percentile-based proxy should give.
CREATE TEMP TABLE leveled AS
SELECT cvegeo, score, ntile(5) OVER (ORDER BY score) AS quintile
FROM composite;

UPDATE ageb_populations a
SET
  socioeconomic_score = l.score,
  estimated_socioeconomic_level = l.quintile - 1  -- ntile is 1..5, enum is Bajo=0..Alto=4
FROM leveled l
WHERE a.cvegeo = l.cvegeo;

COMMIT;

-- Verification: expect ~2496 rows total, ~equal-ish counts across the 5 levels among the scored
-- ones, and a small "unscored" remainder for AGEBs with too little source data.
SELECT
  estimated_socioeconomic_level,
  count(*),
  round(avg(socioeconomic_score)::numeric, 1) AS avg_score
FROM ageb_populations
GROUP BY estimated_socioeconomic_level
ORDER BY estimated_socioeconomic_level NULLS LAST;
