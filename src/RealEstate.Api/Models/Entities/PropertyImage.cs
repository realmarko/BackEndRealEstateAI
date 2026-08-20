namespace RealEstate.Api.Models.Entities;

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public string Url { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}
