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

// Buyer-qualification questions asked when someone contacts an agent/owner about a listing —
// see Inquiry.FundingMethod/Timeline and InquiriesController.
public enum FundingMethod
{
    Cash = 0,
    BankLoan = 1,
    InfonavitFovissste = 2,
    NotSure = 3
}

public enum PurchaseTimeline
{
    ReadyNow = 0,
    OneToThreeMonths = 1,
    ThreeToSixMonths = 2,
    JustBrowsing = 3
}

// See ErrorLog — where an error was caught: an unhandled exception in the API, or a JS error
// the frontend reported itself (nothing here catches the frontend's errors for it).
public enum ErrorSource
{
    Backend = 0,
    Frontend = 1
}

public enum ErrorSeverity
{
    Warning = 0,
    Error = 1,
    Critical = 2
}

// A proxy for AGEB-level socioeconomic status, estimated from public INEGI Census 2020
// indicators (schooling, home internet/car/computer ownership, crowding) via percentile
// ranking within the imported AGEBs. NOT the AMAI NSE classification (A/B/C+/C/D+/D/E) that
// retail chains normally license commercially — that data is proprietary and not something
// this app has access to. Quintile-based by construction (see the AGEB socioeconomic import
// script), so each level holds roughly a fifth of AGEBs with enough source data to score.
public enum SocioeconomicLevel
{
    Bajo = 0,
    MedioBajo = 1,
    Medio = 2,
    MedioAlto = 3,
    Alto = 4
}

// Land tenure classification used in Mexican real estate. Ejidal/Comunal land is held under
// the Ley Agraria's social/communal regime (originally agrarian-reform land) rather than full
// private title, which materially affects whether/how it can be bought, sold, or financed —
// EnRegularizacion covers land in the process of converting from ejidal to private title.
public enum LandTenureType
{
    Privado = 0,
    Ejidal = 1,
    Comunal = 2,
    EnRegularizacion = 3
}

// Simplified single "primary road type" classifier for a lot's main frontage. Deliberately not
// a full per-frontage breakdown (a corner lot can have a different vialidad on each side) — see
// IsCornerLot/StreetFrontageCount on Listing for the corner-lot detail; this enum only captures
// the most relevant frontage for a quick filter/summary.
public enum VialidadType
{
    AvenidaPrincipal = 0,
    CalleSecundaria = 1,
    Privada = 2
}

public enum LotShapeType
{
    Regular = 0,
    Irregular = 1
}

public enum TopographyType
{
    Plana = 0,
    Inclinada = 1
}
