# AGEB socioeconomic proxy import

Adds a proxy socioeconomic estimate per AGEB (`socioeconomic_score`,
`estimated_socioeconomic_level` on `ageb_populations`), built from public INEGI Census 2020
indicators. **This is not the AMAI NSE classification** (A/B/C+/C/D+/D/E) that retail chains use
commercially — that data is proprietary and licensed, not open. `SocioeconomicLevel` in
`Enums.cs` documents the same caveat; keep any UI copy honest about it too.

## Source

INEGI, Censo de Poblacion y Vivienda 2020, "Principales resultados por AGEB y manzana urbana",
state of Puebla (21): https://www.inegi.org.mx/app/scitel/Default?ev=10
(direct file: `resageburb_21csv20.zip`, ~11.3 MB, published 2021-07-26)

## How `ageb_socioeconomic.csv` was derived

From the raw INEGI file (`RESAGEBURB_21CSV20.csv`, ~76k rows — every municipality/locality/AGEB/
block total), kept only the AGEB-level aggregate rows (`NOM_LOC == "Total AGEB urbana"`), built
each row's `cvegeo` as `ENTIDAD + MUN + LOC + AGEB` (matching how `ageb_populations.cvegeo` was
already populated by the original population import), and computed:

| Column | From INEGI columns | Notes |
|---|---|---|
| `avg_schooling_years` | `GRAPROES` | already an average, not a percentage |
| `pct_homes_internet` | `VPH_INTER / TVIVHAB * 100` | |
| `pct_homes_car` | `VPH_AUTOM / TVIVHAB * 100` | |
| `pct_homes_computer` | `VPH_PC / TVIVHAB * 100` | |
| `avg_occupants_per_home` | `PROM_OCUP` | already an average; higher = more crowding |

INEGI masks small-count cells for privacy (`*`, `N/D`); those become empty (NULL), not zero —
~10% of Puebla's 2503 AGEB rows have at least one masked field.

To reproduce this step from a fresh INEGI download for another state, see the `awk` extraction
logic used originally (filter on `NOM_LOC`, build `cvegeo`, guard non-numeric cells) — it isn't
checked in as a standalone script since it was a one-off text transform, not a repeatable tool.

## Running the import

```bash
psql -U <user> -h <host> -d <db> -f import.sql
```

Run from this directory (the script's `\copy` uses a relative path to `ageb_socioeconomic.csv`).
Requires the `AddAgebSocioeconomicData` migration to already be applied. Safe to re-run —
`import.sql` recomputes everything from the CSV each time rather than accumulating.

`import.sql` documents the ranking/quintile logic inline. Summary: each of the 5 indicators is
percentile-ranked independently (so a masked cell in one AGEB doesn't skew others' ranking),
averaged per AGEB (minimum 3 of 5 present, else left unscored), then bucketed into quintiles
(`Bajo` .. `Alto`) over the composite score.

## Extending to other states

Puebla is state code `21`. Adding another state means downloading its own
`resageburb_<code>csv20.zip`, re-running the same extraction, and re-running `import.sql` against
the combined CSV — the quintile buckets are computed across whatever's in the CSV at import time,
so importing states one at a time will re-bucket everyone each time. Re-import all states together
if that matters for consistency.
