// Reads the RUV "Oferta de vivienda" CSV (see README.md Step 1 — download it by hand first,
// this script does not fetch it) and POSTs each row as a FraccionamientoCandidateDto to
// POST /api/fraccionamientos/candidates, reusing the real dedup logic in
// FraccionamientoIngestionService instead of writing a competing SQL import.
//
// NOT YET RUNNABLE AS-IS: COLUMN_MAP below is a guess (see README.md "Open questions") — the
// real CSV has never been opened by this tool (blocked at the network edge every time it was
// tried). Open the real file, find the actual header row, and fix COLUMN_MAP's values (each one
// is the real CSV's column name) before running this for real.
//
// Usage once COLUMN_MAP is confirmed:
//   npm install
//   RUV_CSV_FILE=registro_vivienda_2025_v2.csv \
//   API_BASE_URL=https://api-dev.espacial.com.mx \
//   INGESTION_API_KEY=<the real secret, never commit it> \
//   node convert.js

const fs = require('fs');
const path = require('path');
const https = require('https');
const { parse } = require('csv-parse/sync');

// Each value is the CSV's real header name for that field — CONFIRM AGAINST THE REAL FILE.
const COLUMN_MAP = {
  name: 'TODO_confirm_real_column',          // the conjunto/development's own name
  developerName: 'TODO_confirm_real_column', // empresa/desarrolladora
  state: 'TODO_confirm_real_column',
  city: 'TODO_confirm_real_column',          // municipio
  latitude: 'TODO_confirm_real_column',
  longitude: 'TODO_confirm_real_column',
  stage: null                                 // set to a real column name if one exists; see README
};

// Only these 3 states for now, matching this app's current AGEB/municipality coverage — widen
// later by adding more names here once coverage grows beyond Puebla/Tlaxcala/Querétaro.
const STATE_FILTER = new Set(['Puebla', 'Tlaxcala', 'Querétaro']);

const BATCH_SIZE = 100; // conservative starting point — see README's "Open questions".

function postCandidates(apiBaseUrl, ingestionApiKey, candidates) {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({ candidates });
    const url = new URL('/api/fraccionamientos/candidates', apiBaseUrl);
    const req = https.request(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
        'Content-Length': Buffer.byteLength(body),
        'X-Ingestion-Key': ingestionApiKey
      }
    }, res => {
      let data = '';
      res.on('data', c => data += c);
      res.on('end', () => resolve({ status: res.statusCode, body: data }));
    });
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

(async () => {
  const csvFile = process.env.RUV_CSV_FILE;
  const apiBaseUrl = process.env.API_BASE_URL;
  const ingestionApiKey = process.env.INGESTION_API_KEY;
  if (!csvFile || !apiBaseUrl || !ingestionApiKey) {
    console.error('Set RUV_CSV_FILE, API_BASE_URL, and INGESTION_API_KEY env vars first.');
    process.exit(1);
  }

  const raw = fs.readFileSync(path.join(__dirname, csvFile), 'utf-8');
  const rows = parse(raw, { columns: true, skip_empty_lines: true });
  console.log(`Parsed ${rows.length} rows.`);

  const candidates = [];
  for (const row of rows) {
    const state = row[COLUMN_MAP.state];
    if (!STATE_FILTER.has(state)) continue;

    const lat = parseFloat(row[COLUMN_MAP.latitude]);
    const lng = parseFloat(row[COLUMN_MAP.longitude]);
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) continue; // no usable location, skip

    candidates.push({
      name: row[COLUMN_MAP.name],
      developerName: row[COLUMN_MAP.developerName] || null,
      city: row[COLUMN_MAP.city],
      state,
      latitude: lat,
      longitude: lng,
      stage: COLUMN_MAP.stage ? row[COLUMN_MAP.stage] : null,
      sourceType: 0, // Ruv
      sourceUrl: 'https://www.datos.gob.mx/dataset/oferta_vivienda',
      rawData: JSON.stringify(row)
    });
  }
  console.log(`${candidates.length} candidates in Puebla/Tlaxcala/Querétaro with a usable lat/lng.`);

  let created = 0, matched = 0;
  for (let i = 0; i < candidates.length; i += BATCH_SIZE) {
    const batch = candidates.slice(i, i + BATCH_SIZE);
    const res = await postCandidates(apiBaseUrl, ingestionApiKey, batch);
    if (res.status !== 200) {
      console.error(`Batch ${i / BATCH_SIZE} failed (${res.status}):`, res.body.slice(0, 300));
      continue;
    }
    const result = JSON.parse(res.body);
    created += result.created ?? 0;
    matched += result.matchedExisting ?? 0;
    console.log(`Batch ${i / BATCH_SIZE}: +${result.created ?? 0} new, +${result.matchedExisting ?? 0} matched existing.`);
  }

  console.log(`\nDone. ${created} new Candidate rows created, ${matched} matched an existing record.`);
  console.log('Every new row still needs admin review + Approve before it affects the public site or any score.');
})();
