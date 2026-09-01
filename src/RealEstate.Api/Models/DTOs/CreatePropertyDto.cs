using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

public class CreatePropertyDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public OperationType Operation { get; set; }
    public decimal Price { get; set; }

    public string Address { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty;
    public string City { get; set; } = "Puebla";
    public string State { get; set; } = "Puebla";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double AreaM2 { get; set; }
    public double ConstructionM2 { get; set; }
    public int? ParkingSpots { get; set; }
    public int? YearBuilt { get; set; }

    public int AgentId { get; set; }
    public List<int> AmenityIds { get; set; } = new();
}
