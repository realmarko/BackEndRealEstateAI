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
~10% of Puebla's 2503 AGEB rows have at least one masked field (Tlaxcala, added later: 58 of 668,
~8.7%, a similar rate).

The `awk` command actually used (Tlaxcala, state code `29` — adjust the column-index lookups'
input file and the `2900...` prefix isn't hardcoded, it reads `ENTIDAD` from the file itself):

```bash
awk -F',' '
function numOrEmpty(v) { if (v ~ /^-?[0-9]+(\.[0-9]+)?$/) return v+0; return "" }
NR==1 { for (i=1;i<=NF;i++) { col=$i; gsub(/^\xef\xbb\xbf/,"",col); h[col]=i }; next }
$h["NOM_LOC"] == "Total AGEB urbana" {
  cvegeo = $h["ENTIDAD"] $h["MUN"] $h["LOC"] $h["AGEB"]
  schooling = numOrEmpty($h["GRAPROES"]); occ = numOrEmpty($h["PROM_OCUP"])
  tvivhab = $h["TVIVHAB"]; internet = ""; car = ""; computer = ""
  if (tvivhab ~ /^[0-9]+$/ && tvivhab+0 > 0) {
    vi = numOrEmpty($h["VPH_INTER"]); if (vi != "") internet = sprintf("%.2f", vi/tvivhab*100)
    va = numOrEmpty($h["VPH_AUTOM"]); if (va != "") car = sprintf("%.2f", va/tvivhab*100)
    vp = numOrEmpty($h["VPH_PC"]); if (vp != "") computer = sprintf("%.2f", vp/tvivhab*100)
  }
  print cvegeo "," schooling "," internet "," car "," computer "," occ
}' RESAGEBURB_29CSV20.csv >> ageb_socioeconomic.csv
```

**Line-ending gotcha**: the checked-in `ageb_socioeconomic.csv` uses CRLF throughout (its original
source produced it that way). `awk print` on this project's toolchain emits LF-only. Appending
LF-only rows straight onto a CRLF file produces a file PostgreSQL's `COPY ... CSV` silently
mis-parses once it locks onto CRLF as the row terminator from the first rows — it doesn't error
until well into the mismatched block ("unquoted newline found in data"), which makes the real
cause (two different states' blocks using different line endings) easy to miss. Normalize new rows
to match before appending, e.g. `sed 's/$/\r/' new_rows.csv >> ageb_socioeconomic.csv` — or, if
unsure what the existing file uses, check first: `tr -cd '\r' < ageb_socioeconomic.csv | wc -c`
should equal its line count.

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
