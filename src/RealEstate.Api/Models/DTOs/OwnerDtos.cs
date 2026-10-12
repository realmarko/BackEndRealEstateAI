using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RealEstate.Api.Models.DTOs;

public class CreateOwnerDto
{
    [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Bio { get; set; }

    // Uploaded from the device (multipart/form-data) and stored in S3 — see OwnersController.Create.
    public IFormFile? Photo { get; set; }
}

public class OwnerDto
{
    public int Id { get; set; }
    // Computed server-side from the caller's own JWT — lets the frontend show an Edit button
    // without ever exposing the owner's internal ApplicationUser id to the public API.
    public bool IsOwnProfile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public int PropertiesCount { get; set; }
    public int ViewCount { get; set; }
}

public class OwnerSearchQuery
{
    public string? Name { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ContactOwnerDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(5000)] public string Message { get; set; } = string.Empty;
}
