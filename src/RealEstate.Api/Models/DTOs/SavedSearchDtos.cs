using System.ComponentModel.DataAnnotations;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

public class CreateSavedSearchDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public ListingType? ListingType { get; set; }
    public PropertyType? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }
    public string? City { get; set; }
}

public class SavedSearchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ListingType { get; set; }
    public string? PropertyType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MinBathrooms { get; set; }
    public string? City { get; set; }
    public DateTime CreatedAt { get; set; }
}
