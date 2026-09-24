namespace RealEstate.Api.Models.Entities;

// 1:1 with Listing via a shared primary key (ListingId is both PK and FK) — the standard EF Core
// pattern for a "detail" table split off a wider entity, guaranteeing exactly one address per
// listing at the DB level instead of needing a separate unique index.
public class ListingAddress
{
    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string Street { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = "México";

    // Short line for plain-text email bodies (inquiry fact-sheet, saved-search alerts) — the
    // one place both controllers built this the same way, now shared instead of duplicated.
    public string ToEmailLine() => $"{Street}, {Colonia}, {City}";
}
