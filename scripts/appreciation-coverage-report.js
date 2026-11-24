// Reports how much of the "Potencial de plusvalía (2 años)" layer's data coverage actually
// exists today, without calling the live scoring endpoint (which would hit Overpass for every
// AGEB — slow and inconsiderate to that free, shared service). Instead it answers the cheaper,
// still-useful question: "which AGEBs have a published Fraccionamiento or InfrastructureProject
// in range at all?" — those are the only two signals (weights 35+15, more than half the total)
// that depend on admin-entered data rather than connectivity/listings/census data this app
// already has. An AGEB with neither can score at most ~50 (connectivity alone, if that even
// succeeds) and will often still show "sin datos suficientes".
//
// Usage:
//   node appreciation-coverage-report.js [apiBaseUrl]
// Defaults to https://api-dev.espacial.com.mx — pass https://api.espacial.com.mx to check prod.
//
// This is the durable way to answer "which zones have I already covered?" without relying on
// anyone's memory of past sessions — rerun it any time. For a single zone, the admin panel
// (`/admin/fraccionamientos`, `/admin/infrastructure-projects`) and the map's own appreciation
// layer tooltip (click a zone — its explanations name the exact development/project that fired)
// are more direct than this script.

const https = require('https');

const API_BASE_URL = process.argv[2] || 'https://api-dev.espacial.com.mx';

function get(url) {
  return new Promise((resolve, reject) => {
    https.get(url, res => {
      let data = '';
      res.on('data', c => data += c);
      res.on('end', () => resolve(data));
    }).on('error', reject);
  });
}

function centroid(geom) {
  // Flattens all ring coordinates and averages them — a rough centroid, fine for ranking
  // coverage, NOT a replacement for the real area-weighted centroid the scoring service itself
  // uses server-side.
  const pts = [];
  (function walk(c) {
    if (typeof c[0] === 'number') { pts.push(c); return; }
    for (const x of c) walk(x);
  })(geom.coordinates);
  let sx = 0, sy = 0;
  for (const [x, y] of pts) { sx += x; sy += y; }
  return { lng: sx / pts.length, lat: sy / pts.length };
}

function distMeters(lat1, lng1, lat2, lng2) {
  const R = 6371000;
  const toRad = d => d * Math.PI / 180;
  const dLat = toRad(lat2 - lat1);
  const dLng = toRad(lng2 - lng1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2;
  return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

// Covers Puebla(21) + Tlaxcala(29) + Querétaro(22), the 3 states with AGEB data loaded as of
// 2026-11-24 — extend this if/when another state's census data gets imported (see
// database/data-imports/ageb-boundaries). /agebs caps bbox size at 2° per side, hence the tiling.
const TILES = [
  [17.8, -99.1, 19.8, -97.1], [17.8, -97.1, 19.8, -96.7],
  [19.8, -99.1, 20.9, -97.1], [19.8, -97.1, 20.9, -96.7], // Puebla
  [19.0, -98.7, 19.7, -97.5],                              // Tlaxcala
  [19.9, -100.6, 21.7, -99.0]                               // Querétaro
];

(async () => {
  const municisBody = await get(`${API_BASE_URL}/api/geomarketing/municipalities`);
  const municipios = new Map(JSON.parse(municisBody).map(m => [m.cvegeo, m.name]));

  const agebMap = new Map();
  for (const [swLat, swLng, neLat, neLng] of TILES) {
    const body = await get(`${API_BASE_URL}/api/geomarketing/agebs?swLat=${swLat}&swLng=${swLng}&neLat=${neLat}&neLng=${neLng}`);
    let arr;
    try { arr = JSON.parse(body); } catch (e) { console.error('parse fail', body.slice(0, 200)); continue; }
    if (!Array.isArray(arr)) { console.error('tile err', JSON.stringify(arr).slice(0, 200)); continue; }
    for (const a of arr) {
      if (agebMap.has(a.cvegeo)) continue;
      const c = centroid(a.boundary);
      agebMap.set(a.cvegeo, { cvegeo: a.cvegeo, lat: c.lat, lng: c.lng });
    }
  }

  const fraccBody = await get(`${API_BASE_URL}/api/fraccionamientos/published?pageSize=100`);
  const fracc = JSON.parse(fraccBody).items;
  const infraBody = await get(`${API_BASE_URL}/api/infrastructure-projects/published`);
  const infra = JSON.parse(infraBody);

  console.log(`Against ${API_BASE_URL} — ${agebMap.size} AGEBs, ${fracc.length} published fraccionamientos, ${infra.length} infra projects.\n`);

  let hasDev = 0, hasInfra = 0, hasEither = 0;
  const neitherByMunicipio = new Map(); // cvegeo(municipio) -> count of uncovered AGEBs

  for (const ageb of agebMap.values()) {
    const nearestDev = fracc.reduce((min, f) => Math.min(min, distMeters(ageb.lat, ageb.lng, f.latitude, f.longitude)), Infinity);
    const nearestInfra = infra.reduce((min, p) => Math.min(min, distMeters(ageb.lat, ageb.lng, p.latitude, p.longitude) - p.affectedRadiusMeters), Infinity);
    const devOk = nearestDev <= 2000;
    const infraOk = nearestInfra <= 0;
    if (devOk) hasDev++;
    if (infraOk) hasInfra++;
    if (devOk || infraOk) { hasEither++; continue; }

    const municipioCvegeo = ageb.cvegeo.slice(0, 5);
    neitherByMunicipio.set(municipioCvegeo, (neitherByMunicipio.get(municipioCvegeo) ?? 0) + 1);
  }

  const total = agebMap.size;
  console.log(`With a fraccionamiento within 2km: ${hasDev} (${(100 * hasDev / total).toFixed(1)}%)`);
  console.log(`With an infra project in range:    ${hasInfra} (${(100 * hasInfra / total).toFixed(1)}%)`);
  console.log(`With EITHER (covered):              ${hasEither} (${(100 * hasEither / total).toFixed(1)}%)`);
  console.log(`With NEITHER (ceiling ~50, often "sin datos"): ${total - hasEither} (${(100 * (total - hasEither) / total).toFixed(1)}%)`);

  const worst = [...neitherByMunicipio.entries()].sort((a, b) => b[1] - a[1]).slice(0, 20);
  console.log('\nTop 20 least-covered municipios (most uncovered AGEBs first) — good targets for the next research round:');
  for (const [cvegeo, count] of worst) {
    console.log(`  ${count.toString().padStart(4)} AGEBs  ${municipios.get(cvegeo) ?? cvegeo} (${cvegeo})`);
  }
})();
