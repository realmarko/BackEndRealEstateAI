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

    // DB-computed (GENERATED ALWAYS AS lower(city) STORED — see
    // ApplicationDbContext.OnModelCreating) and indexed. ListingsController's city filter and
    // "similar listings" query compare against this instead of calling .ToLower() on City at
    // query time — a case-insensitive match that way requires an index on lower(city) anyway, so
    // this both avoids the runtime function call and gives the index something exact to match.
    // Never set from app code.
    public string CityLower { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = "México";

    // Short line for plain-text email bodies (inquiry fact-sheet, saved-search alerts) — the
    // one place both controllers built this the same way, now shared instead of duplicated.
    public string ToEmailLine() => $"{Street}, {Colonia}, {City}";
}
