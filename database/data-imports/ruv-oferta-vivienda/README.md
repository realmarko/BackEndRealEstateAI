# RUV "Oferta de vivienda" import (plan — not yet runnable)

Feeds `fraccionamientos`/`fraccionamiento_sources` with real, officially-registered housing
developments nationwide — the "Desarrollos nuevos" signal in
`AppreciationPotentialService.AddNewDevelopmentSignalAsync` (weight 35, the heaviest) currently
only has data for the handful of zones someone manually researched and entered via the admin.
This is the single highest-leverage source to close that gap: it's official (Registro Único de
Vivienda, via SEDATU), nationwide (not just Puebla/Tlaxcala/Querétaro), and legally reusable
(Creative Commons Attribution 4.0) — unlike scraping Lamudi/Inmuebles24/Vivanuncios, which would
need per-site maintenance and risks ToS/anti-bot issues for an ongoing pipeline.

**Why "plan" and not a working import, unlike `../ageb-boundaries/` or `../municipal-boundaries/`:**
the actual CSV file is served from `repodatos.atdt.gob.mx` behind what's almost certainly an
Akamai WAF — every automated fetch attempt (`curl` with/without a browser User-Agent, a `fetch()`
from the hosting page itself, and a fresh browser tab's own full navigation) came back `403
Access Denied` from `errors.edgesuite.net` (checked 2026-11-24). **A real person opening the link
in their own everyday browser should be unaffected** — this kind of block almost always targets
automated clients, not human traffic — but that means step 1 below has to be done by hand, not by
a script, and this plan's column mapping below is **unconfirmed** until someone does that and
shares the real header row.

## Source

- Dataset page: https://www.datos.gob.mx/dataset/oferta_vivienda
- Specific resource ("Registro de vivienda en el Registro Único de Vivienda (RUV)..."):
  https://www.datos.gob.mx/dataset/oferta_vivienda/resource/eae260c3-0c6a-475e-8aa9-ae57b79b9507
- Direct file link on that page ("Descargar recurso" / "Descargar"):
  `https://repodatos.atdt.gob.mx/api_update/secretaria_desarrollo_territorial/oferta_vivienda/registro_vivienda_2025_v2.csv`
- Publisher: Secretaría de Desarrollo Agrario, Territorial y Urbano (SEDATU)
- License: Creative Commons Attribution 4.0
- **Update frequency: annual** (last updated 2026-06-25 per the dataset page) — this alone means
  it should be a periodic manual import like the INEGI ones in this folder's siblings, never a
  live/scheduled n8n pipeline; there would be nothing new to fetch between runs most of the year.
- Per the related Banco de México EconLab data sheet (the closest independent description found,
  since the RUV portal itself has no public schema docs): the dataset covers "proyectos de oferta
  de vivienda nueva y existente en conjuntos habitacionales y en el mercado abierto individual",
  including "ubicación con geolocalización" — i.e. it should have per-project lat/lng, not just a
  municipio name, which is exactly what `FraccionamientoCandidateDto.Latitude`/`Longitude` needs.

## Step 1 — get the real file (manual, by a human, in a normal browser)

1. Open https://www.datos.gob.mx/dataset/oferta_vivienda/resource/eae260c3-0c6a-475e-8aa9-ae57b79b9507
   in a regular browser (not this automation) and click "Descargar" / "Descargar recurso".
2. Save the CSV into this folder (gitignored — same pattern as the raw shapefiles in
   `../ageb-boundaries/` and `../municipal-boundaries/`; only the derived, already-reviewed output
   gets committed).
3. Open it and note the actual header row. Expected-but-unconfirmed columns, based on the
   Banxico description and the RUV registration manuals' own field names
   (`02_REGISTRO_DE_OFERTA_EN_CONJUNTO_SEMBRADO_DE_VIVIENDA.pdf`,
   `04_REGISTRO_DE_UBICACIÓN_DE_OFERTA.pdf` on `portal.ruv.org.mx`): development/conjunto name,
   developer/empresa name, state, municipio, latitude, longitude, and some registration/offer
   date. **Do not assume these exact column names in `convert.js` below until confirmed against
   the real header** — update the `COLUMN_MAP` constant at the top of the script to match.

## Step 2 — map rows to `FraccionamientoCandidateDto` and POST them (not a raw SQL import)

Unlike this folder's INEGI siblings (plain reference data, no dedup needed — a straight
`INSERT ... ON CONFLICT DO UPDATE`), `fraccionamientos` has real per-row business logic that must
run for every candidate: `FraccionamientoIngestionService.IngestOneInternalAsync` matches against
existing records within 500m + 60% name similarity before deciding whether a row is a duplicate or
a new development (see that file's own comments). **Don't reimplement that matching in SQL** —
reuse it exactly as every other source does, by POSTing to the existing
`POST /api/fraccionamientos/candidates` endpoint (`FraccionamientosController.IngestCandidates`),
batched, with `SourceType: Ruv` (enum value `0`, see `FraccionamientoSource.cs`) on each row. This
lands every row as a `Candidate` — same as a scraped detection — still requiring an admin to
`Approve` it before it's public, exactly like the 6 pre-existing unpublished candidates reviewed
by hand on 2026-11-24 (two had bad data and needed correcting before approval). **Don't
auto-approve rows from this import** — same verification discipline applies to a government CSV
row as to anything else; a ~20k-row national dataset should eyeball-spot-check a sample for
plausibility before anyone bulk-approves it, not approve blind.

`convert.js` (to write once Step 1's real columns are known) should, per the pattern in
`../ageb-boundaries/convert.js`:
1. Parse the CSV (a plain line-delimited CSV reader is enough — no shapefile/projection work
   needed this time, unlike the geographic imports in this folder).
2. Filter to the 3 states this app currently covers (Puebla `21`, Tlaxcala `29`, Querétaro `22`) —
   or all of them, now that the goal is explicitly broader coverage; either is a one-line filter
   change later, so don't over-decide this up front.
3. Build `FraccionamientoCandidateDto` JSON bodies (`Name`, `DeveloperName`, `City`, `State`,
   `Latitude`, `Longitude`, `Stage` if the source has an equivalent field, `SourceType: 0`,
   `SourceUrl`: the dataset page URL above — a real attributable source, not a fabricated one).
4. POST in batches (the controller takes a list per call) to
   `{apiUrl}/api/fraccionamientos/candidates` with header `X-Ingestion-Key: <the real secret>`.

## Step 3 — the ingestion key has to actually be set first

`Fraccionamiento:IngestionApiKey` (`FraccionamientoOptions.cs`) is still the source-code default
placeholder in both dev and prod — confirmed 2026-11-24 via `aws elasticbeanstalk
describe-configuration-settings` on `realestateapp-dev`: no `Fraccionamiento__IngestionApiKey` env
var exists among its 26 configured settings. **This means the whole scraping pipeline (not just
this RUV import) has never been connected to anything real in either environment** — every
"real" fraccionamiento in the dev database today was entered by hand (`SourceType: ManualAdmin`)
or inserted directly, never through this endpoint. Generate a real secret and set it via
`dotnet user-secrets` locally / the EB environment config for both `realestateapp-dev` and
`realestateapp-prod` (the service role boundary note in `../../AGENTS.md`-equivalent project
memory applies the same way it does to every other real secret in this app) before running
`convert.js`'s output against either environment.

## Open questions for whoever picks this up

- Confirm the real column names (Step 1) — everything above downstream of that is still a guess.
- Does the dataset include a "stage" equivalent (preventa / en construcción / entrega inmediata)?
  If not, every RUV-sourced candidate will need `Stage: null`, which
  `AddNewDevelopmentSignalAsync`'s `StageWeight` helper already handles (defaults to full weight
  when `Stage` doesn't match "Entrega inmediata" case-insensitively) — not a blocker, just note it.
- ~20k+ rows nationwide (based on the live counter on `portal.ruv.org.mx`: "Vivienda nueva en
  conjunto: 5,117,532" units, almost certainly across far fewer *conjuntos* than that — the real
  per-project row count is unknown until the file is open) means Step 2's POST batching needs a
  sane batch size; the controller itself has no documented cap, so start conservative (e.g. 100
  rows/call) and watch for timeouts before assuming larger batches are safe.
