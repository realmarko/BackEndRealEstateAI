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
    Land = 4,
    Commercial = 5,
    ResidentialLand = 6,
    Ranch = 7,
    Office = 8,
    IndustrialWarehouse = 9,
    CommercialLand = 10,
    IndustrialStorage = 11,
    RetailSpace = 12,
    Building = 13,
    Room = 14,
    CommercialStorage = 15,
    IndustrialLand = 16
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
