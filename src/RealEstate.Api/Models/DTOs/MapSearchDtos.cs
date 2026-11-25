namespace RealEstate.Api.Models.DTOs;

public class MapSearchParseRequestDto
{
    public string Query { get; set; } = string.Empty;
}

// All-nullable, same convention as ListingDraftParseResultDto — the model only fills in what the
// query actually implies. ListingType/PropertyType come back as the C# enum member name (e.g.
// "Sale", "House"), matching ListingDraftParseResultDto so the frontend can reuse the very same
// *_FROM_STRING maps. MinBedrooms/MinBathrooms are a floor ("3 recámaras" -> at least 3), not an
// exact count, mirroring the map's own beds/baths filters.
public class MapSearchParseResultDto
{
    public string? ListingType { get; set; }
    public string? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }
    public string? City { get; set; }
    // Short human-readable restatement of what was understood (e.g. "Casas en venta en Cholula,
    // 3+ recámaras, hasta $3,000,000"), shown to the visitor so they can tell the search worked as
    // intended — null when the query wasn't a real-estate search or nothing could be parsed.
    public string? Interpretation { get; set; }
}
