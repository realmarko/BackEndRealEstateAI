namespace RealEstate.Api.Models.DTOs;

// The mirror image of ListingDraftParseRequestDto/ResultDto: instead of free text -> structured
// fields, this goes structured fields -> a drafted description. Every field nullable/optional —
// an owner can click "Generar con IA" partway through filling the form, so this has to work from
// whatever's already filled in, not demand every field first. Unlike ListingDraftParseResultDto
// (backend -> frontend, PascalCase enum names), this is frontend -> backend: ListingType is sent
// as the form's own lowercase token ("rent"/"sale", matched case-insensitively — see
// ListingDescriptionGenerationService.BuildFactsList) and PropertyType/ConstructionCondition are
// sent as their already-translated Spanish display label (e.g. "Casa"), not an enum code — these
// two are quoted straight into the generated description's facts list, so a human-readable label
// reads naturally there, unlike a raw "house"/"land" token would.
public class GenerateListingDescriptionRequestDto
{
    public string? ListingType { get; set; }
    public string? PropertyType { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? Colonia { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public decimal? AreaSqm { get; set; }
    public decimal? LotSizeSqm { get; set; }
    public int? ParkingSpaces { get; set; }
    public int? Floors { get; set; }
    public int? YearBuilt { get; set; }
    public string? ConstructionCondition { get; set; }
    public bool? HasPool { get; set; }
    public bool? HasRoofGarden { get; set; }
    public bool? HasTerrace { get; set; }
    public bool? HasBalcony { get; set; }
    public bool? HasSecurity { get; set; }
    public bool? HasElevator { get; set; }
    public bool? HasGym { get; set; }
    public bool? HasBbqArea { get; set; }
    public bool? HasNaturalGas { get; set; }
    public bool? HasWell { get; set; }
    public bool? IsCornerLot { get; set; }
    // Land/commercial services checklist — same fields as the parse-draft DTO, since an empty lot
    // listing has almost none of the house-only fields above filled in.
    public bool? HasPotableWater { get; set; }
    public bool? HasDrainage { get; set; }
    public bool? HasElectricity { get; set; }
    public bool? HasPavedStreet { get; set; }
}

public class GenerateListingDescriptionResultDto
{
    public string Description { get; set; } = string.Empty;
}
