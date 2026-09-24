// Reads INEGI's 21mun.shp (municipal boundaries, Puebla), reprojects every ring from the source
// Lambert Conformal Conic (ITRF2008 / GRS80) to WGS84 (EPSG:4326), and writes import.sql —
// matching this project's existing data-imports convention (see
// ../ageb-socioeconomic/import.sql). See README.md for where to get 21mun.shp/.dbf/.prj and how
// to run this.
const shapefile = require("shapefile");
const proj4 = require("proj4");
const fs = require("fs");
const path = require("path");

// Exact params from 21mun.prj (Lambert_Conformal_Conic, ITRF2008/GRS80) — do not change without
// re-checking the .prj that ships with whatever INEGI edition is being imported.
const SOURCE_PROJ =
  "+proj=lcc +lat_1=17.5 +lat_2=29.5 +lat_0=12 +lon_0=-102 +x_0=2500000 +y_0=0 " +
  "+ellps=GRS80 +units=m +no_defs";
const TARGET_PROJ = "+proj=longlat +datum=WGS84 +no_defs";
const reproject = proj4(SOURCE_PROJ, TARGET_PROJ);

function reprojectRing(ring) {
  return ring.map(([x, y]) => {
    const [lng, lat] = reproject.forward([x, y]);
    return `${lng.toFixed(7)} ${lat.toFixed(7)}`;
  });
}

// GeoJSON (what the `shapefile` package emits per feature) Polygon/MultiPolygon coordinates ->
// WKT, reprojecting every ring. INEGI municipal polygons can be multi-part (islands, exclaves).
function coordsToWkt(geometry) {
  if (geometry.type === "Polygon") {
    const rings = geometry.coordinates.map((ring) => `(${reprojectRing(ring).join(", ")})`);
    return `POLYGON(${rings.join(", ")})`;
  }
  if (geometry.type === "MultiPolygon") {
    const polys = geometry.coordinates.map(
      (poly) => `(${poly.map((ring) => `(${reprojectRing(ring).join(", ")})`).join(", ")})`
    );
    return `MULTIPOLYGON(${polys.join(", ")})`;
  }
  throw new Error(`Unexpected geometry type: ${geometry.type}`);
}

function sqlString(value) {
  return `'${String(value).replace(/'/g, "''")}'`;
}

async function main() {
  const shpPath = path.join(__dirname, "21mun.shp");
  const dbfPath = path.join(__dirname, "21mun.dbf");
  const source = await shapefile.open(shpPath, dbfPath, { encoding: "latin1" });

  const rows = [];
  let result = await source.read();
  while (!result.done) {
    const { properties, geometry } = result.value;
    rows.push({ properties, wkt: coordsToWkt(geometry) });
    result = await source.read();
  }

  console.log(`Read ${rows.length} municipality polygons.`);

  const outPath = path.join(__dirname, "import.sql");
  const lines = [];
  lines.push("BEGIN;");
  lines.push("CREATE TEMP TABLE staging_municipal_boundaries (");
  lines.push("  cvegeo varchar(5) PRIMARY KEY,");
  lines.push("  name varchar(100) NOT NULL,");
  lines.push("  state_name varchar(100) NOT NULL,");
  lines.push("  boundary geometry NOT NULL");
  lines.push(");");
  lines.push("");

  for (const row of rows) {
    const p = row.properties;
    // CVE_ENT + CVE_MUN per INEGI's municipios.csv layout; NOMGEO carries the name.
    const cveEnt = p.CVE_ENT ?? p.CVEGEO?.slice(0, 2);
    const cveMun = p.CVE_MUN ?? p.CVEGEO?.slice(2, 5);
    const cvegeo = `${cveEnt}${cveMun}`;
    const name = p.NOMGEO ?? p.NOM_MUN ?? p.NOMBRE;
    lines.push(
      `INSERT INTO staging_municipal_boundaries (cvegeo, name, state_name, boundary) VALUES (` +
        `${sqlString(cvegeo)}, ${sqlString(name)}, ${sqlString("Puebla")}, ` +
        `ST_SetSRID(ST_GeomFromText(${sqlString(row.wkt)}), 4326));`
    );
  }

  lines.push("");
  lines.push("-- Idempotent: replace on cvegeo conflict rather than accumulating duplicates.");
  lines.push("INSERT INTO municipal_boundaries (cvegeo, name, state_name, boundary)");
  lines.push("SELECT cvegeo, name, state_name, boundary FROM staging_municipal_boundaries");
  lines.push("ON CONFLICT (cvegeo) DO UPDATE SET");
  lines.push("  name = EXCLUDED.name, state_name = EXCLUDED.state_name, boundary = EXCLUDED.boundary;");
  lines.push("");
  lines.push("SELECT count(*) AS imported_count FROM municipal_boundaries;");
  lines.push("COMMIT;");

  fs.writeFileSync(outPath, lines.join("\n"), "utf8");
  console.log(`Wrote ${outPath}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
