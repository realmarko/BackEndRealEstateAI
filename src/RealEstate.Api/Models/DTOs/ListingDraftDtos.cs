using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class ListingDraftParseRequestDto
{
    // A pasted classified-ad text (WhatsApp/Facebook-style listing copy) — 8000 chars is
    // generous for even a long ad and bounds both the AI call's cost and abuse of the endpoint.
    [Required, MaxLength(8000)] public string Text { get; set; } = string.Empty;
}

// Every field nullable by design: this is a best-effort extraction from free text, not a
// validated listing — ListingDraftParsingService only sets a field when the source text states
// it explicitly or it's a direct unit conversion, leaving the rest null rather than guessing.
// The frontend only patches the fields that come back non-null, so an owner's own in-progress
// edits are never silently overwritten. Mirrors the subset of ListingCreateDto that free-form
// ad copy realistically mentions — no legal/zoning/cadastral fields, which almost never appear
// in a WhatsApp listing.
public class ListingDraftParseResultDto
{
    // ListingType/PropertyType/ConstructionCondition are the enum's .ToString() name (e.g.
    // "House", "Sale"), not the numeric value — matching ListingDto's own convention (see its
    // own string ListingType/PropertyType) so the frontend's existing *_FROM_STRING maps
    // (listing-api.adapter.ts) can be reused as-is instead of needing a second, numeric mapping
    // just for this endpoint.
    public string? Title { get; set; }
    public string? ListingType { get; set; }
    public string? PropertyType { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Colonia { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    // Square meters, same unit the frontend form's own areaSqm/lotSizeSqm controls use — unlike
    // ListingCreateDto.AreaSqFt, no unit conversion is needed on the way into the form.
    public decimal? AreaSqm { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public int? ParkingSpaces { get; set; }
    public int? Floors { get; set; }
    public int? YearBuilt { get; set; }
    public string? ConstructionCondition { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasRoofGarden { get; set; }
    public bool? HasTerrace { get; set; }
    public bool? HasLaundryArea { get; set; }
    public bool? HasCistern { get; set; }
    public bool? HasNaturalGas { get; set; }
    public bool? HasLpGas { get; set; }
    public bool? HasSecurity { get; set; }
    public bool? HasElevator { get; set; }
    public bool? HasGym { get; set; }
    public bool? HasBbqArea { get; set; }
    public bool? HasServiceRoom { get; set; }
    public bool? HasStorageRoom { get; set; }
    public bool? HasPropertyTaxDebt { get; set; }

    // Resolved separately from everything else above (see GoogleMapsLinkResolverService) when
    // the pasted text includes a Google Maps link — not something the AI extraction itself can
    // determine, since turning a link (or the place name it resolves to) into real coordinates
    // needs an actual HTTP/geocoding call, not text understanding.
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    // True when the text had a Google Maps link but it couldn't be turned into coordinates
    // (broken/mangled link, an unrecognized share format, a failed redirect/geocode) — distinct
    // from Lat/Lng simply being null because no link was there at all, so the frontend can warn
    // the owner instead of silently leaving the location unset.
    public bool MapsLinkResolutionFailed { get; set; }
}
