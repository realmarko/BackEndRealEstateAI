namespace RealEstate.Api.Services;

// A server-side Google Maps key, deliberately separate from the browser-exposed one the frontend
// ships (environments/env-keys.ts) — that one is restricted by HTTP referrer, which only applies
// to requests a browser makes with a matching Referer header. A server-side call (no browser,
// no referrer) against a referrer-restricted key is rejected, so Maps Static API needs its own
// key, restricted by API (Maps Static API only) and optionally by server IP instead of referrer.
public class GoogleMapsOptions
{
    public string StaticMapsApiKey { get; set; } = "CHANGE_ME_GOOGLE_STATIC_MAPS_API_KEY";

    // Separate key, restricted to the Routes API only (same referrer-vs-server-IP reasoning as
    // StaticMapsApiKey above) — used by GoogleTrafficService for the live traffic-congestion
    // check in the commercial-viability analysis. Routes API with traffic awareness is a paid,
    // billed-per-request SKU (unlike Static Maps' free tier), so this key needs its own budget
    // alert in Google Cloud once set — see GoogleTrafficService for the caching that bounds call
    // volume.
    public string RoutesApiKey { get; set; } = "CHANGE_ME_GOOGLE_ROUTES_API_KEY";

    // Separate key, restricted to the Geocoding API only (same reasoning as the two keys above)
    // — used by GoogleMapsLinkResolverService to turn the human-readable place name a shared
    // Google Maps link resolves to (e.g. "Residencial Tlatelcos, 24 ote #1227...") into real
    // lat/lng when the link itself doesn't already embed coordinates (most "place" share links
    // from the Maps app resolve to a CID reference, not a @lat,lng URL).
    public string GeocodingApiKey { get; set; } = "CHANGE_ME_GOOGLE_GEOCODING_API_KEY";
}
