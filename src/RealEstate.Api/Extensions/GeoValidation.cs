namespace RealEstate.Api.Extensions;

// Shared by every controller that accepts caller-supplied lat/lng query parameters
// (GeomarketingController, ListingsController's bounding-box filter). .NET's query-string double
// binder accepts NaN, Infinity, and any out-of-range number as a "valid" double, so without this
// check malformed input sails straight into a spatial query or filter that can only ever fail or
// return nonsense — one shared place to fix means a new lat/lng-accepting endpoint gets this for
// free instead of needing its own copy (and its own chance to forget it).
public static class GeoValidation
{
    public static bool IsValidLat(double lat) => !double.IsNaN(lat) && !double.IsInfinity(lat) && lat is >= -90 and <= 90;
    public static bool IsValidLng(double lng) => !double.IsNaN(lng) && !double.IsInfinity(lng) && lng is >= -180 and <= 180;

    // Convenience for the common single-point case (e.g. GeomarketingController); a caller
    // validating several named fields (e.g. a bounding box's four corners) should call
    // IsValidLat/IsValidLng directly so each failure can name its own field.
    public static string? ValidateLatLng(double lat, double lng)
    {
        if (!IsValidLat(lat)) return "lat must be a finite number between -90 and 90.";
        if (!IsValidLng(lng)) return "lng must be a finite number between -180 and 180.";
        return null;
    }
}
