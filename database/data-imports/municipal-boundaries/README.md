# Municipal boundaries import

Populates `municipal_boundaries` (one row per municipality, complete urban+rural polygon) —
the layer to use for "is point X inside municipality Y" queries. Not to be confused with
`ageb_populations` (see `../ageb-socioeconomic/`), which only covers *urban* AGEB polygons and
would silently miss a municipality's rural edges if aggregated to approximate a municipal
boundary.

## Source

INEGI, Marco Geoestadístico, Censo de Población y Vivienda 2020, state of Puebla (21):
https://www.inegi.org.mx/app/biblioteca/ficha.html?upc=889463807469
(direct file: `21_puebla.zip`, ~152 MB, published 2021-01-21)

The ZIP bundles several shapefiles under `conjunto_de_datos/`; this import only uses
`21mun.shp`/`.dbf`/`.prj`/`.shx`/`.cpg` — **"eemun" = Áreas geoestadísticas municipales** per the
bundle's own `catalogos/contenido.txt`. (The same ZIP also has `21m.shp`, the actual "manzana"
— block — level, one step finer than AGEB; not imported here or anywhere else in the project yet.)

Source shapefiles are in **Lambert Conformal Conic** (datum ITRF2008/GRS80), not lat/lng —
`convert.js` reprojects every ring to WGS84 (EPSG:4326) using the exact parameters from
`21mun.prj`:
```
+proj=lcc +lat_1=17.5 +lat_2=29.5 +lat_0=12 +lon_0=-102 +x_0=2500000 +y_0=0 +ellps=GRS80 +units=m
```

## Regenerating `import.sql` from a fresh download

The raw shapefile (`21mun.*`, ~5.8 MB) isn't checked in — only the derived `import.sql` (WKT
already reprojected, human-readable/diffable) is. To regenerate:

1. Download `21_puebla.zip` from the URL above.
2. Extract just `conjunto_de_datos/21mun.cpg`, `.dbf`, `.prj`, `.shp`, `.shx` into this directory.
3. `npm install` (installs `shapefile` and `proj4` — both pure JS, no GDAL/native deps needed;
   this project doesn't have GDAL installed and this avoids requiring it).
4. `npm run convert` — reads `21mun.shp`, writes `import.sql`.

## Running the import

```bash
psql -U <user> -h <host> -d <db> -f import.sql
```

Requires the `AddMunicipalBoundaries` migration to already be applied. Safe to re-run —
upserts on `cvegeo` via `ON CONFLICT DO UPDATE` rather than accumulating duplicates.

## Extending to other states

Puebla is state code `21`, and its ZIP happens to ship all 217 of its own municipalities in one
`21mun.shp`. Adding another state means downloading its own `<code>_<state>.zip`, extracting that
state's `<code>mun.shp`, and re-running `convert.js` against it — `state_name` is currently
hardcoded to `"Puebla"` in `convert.js`, so that line needs updating (or parameterizing) before
importing a second state.
