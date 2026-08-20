namespace RealEstate.Api.Models.Entities;

public class Property
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PropertyType Type { get; set; } // Casa, Departamento, Terreno, Local
    public OperationType Operation { get; set; } // Venta, Renta
    public decimal Price { get; set; }
    public string Currency { get; set; } = "MXN";

    // Ubicación
    public string Address { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = string.Empty; // Colonia
    public string City { get; set; } = "Puebla";
    public string State { get; set; } = "Puebla";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Características
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double AreaM2 { get; set; }
    public double ConstructionM2 { get; set; }
    public int? ParkingSpots { get; set; }
    public int? YearBuilt { get; set; }

    // Estado y metadata
    public PropertyStatus Status { get; set; } // Disponible, Vendida, Rentada
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? SourceUrl { get; set; } // útil si luego capturas con IA

    // Relaciones
    public List<PropertyImage> Images { get; set; } = new();
    public int AgentId { get; set; }
    public Agent? Agent { get; set; }
}
