namespace RealEstate.Api.Models.Entities;

public class Amenity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // Alberca, Jardín, Gimnasio, etc.

    public List<Property> Properties { get; set; } = new();
}
