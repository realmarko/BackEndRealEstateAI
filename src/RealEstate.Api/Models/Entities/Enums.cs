namespace RealEstate.Api.Models.Entities;

public enum ListingType
{
    Sale = 0,
    Rent = 1
}

public enum PropertyType
{
    House = 0,
    Apartment = 1,
    Condo = 2,
    Townhouse = 3,
    Land = 4
}

public enum ListingStatus
{
    Active = 0,
    Pending = 1,
    SoldOrRented = 2,
    Removed = 3
}

public enum OperationType
{
    Sale = 0,
    Rent = 1
}

public enum PropertyStatus
{
    Available = 0,
    Sold = 1,
    Rented = 2
}
