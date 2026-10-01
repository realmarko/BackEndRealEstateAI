using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RealEstate.Api.Models.DTOs;

public class BrokerageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public int AgentsCount { get; set; }
    public int ListingsCount { get; set; }
    // True only for an agent whose own profile belongs to this brokerage — gates the "edit my
    // agency" entry point client-side (the real check is still enforced server-side on Update).
    public bool CanEdit { get; set; }
}

public class BrokerageDirectoryQuery
{
    public string? Name { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class UpdateBrokerageProfileDto
{
    [MaxLength(150)] public string? State { get; set; }
    [MaxLength(150)] public string? City { get; set; }
    [MaxLength(2048)] public string? Website { get; set; }
    [MaxLength(3000)] public string? Description { get; set; }
    [MaxLength(2048)] public string? FacebookUrl { get; set; }
    [MaxLength(2048)] public string? InstagramUrl { get; set; }

    // Uploaded from the device (multipart/form-data) and stored in S3 — see
    // AgentsController.TryUploadPhotoAsync for the identical pattern.
    public IFormFile? Logo { get; set; }
}
