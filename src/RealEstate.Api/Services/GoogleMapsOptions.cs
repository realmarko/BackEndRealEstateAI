namespace RealEstate.Api.Services;

// A server-side Google Maps key, deliberately separate from the browser-exposed one the frontend
// ships (environments/env-keys.ts) — that one is restricted by HTTP referrer, which only applies
// to requests a browser makes with a matching Referer header. A server-side call (no browser,
// no referrer) against a referrer-restricted key is rejected, so Maps Static API needs its own
// key, restricted by API (Maps Static API only) and optionally by server IP instead of referrer.
public class GoogleMapsOptions
{
    public string StaticMapsApiKey { get; set; } = "CHANGE_ME_GOOGLE_STATIC_MAPS_API_KEY";
}
