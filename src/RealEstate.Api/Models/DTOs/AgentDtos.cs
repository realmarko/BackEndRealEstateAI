using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RealEstate.Api.Models.DTOs;

public class CreateAgentDto
{
    [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [MaxLength(200)] public string? Company { get; set; }
    public bool IsIndependent { get; set; }
    [MaxLength(2000)] public string? Bio { get; set; }

    // Comma-separated from the form (e.g. "Buyer's Agent, Staging, Relocation") — split in the controller.
    [MaxLength(500)] public string? Specialties { get; set; }

    // Uploaded from the device (multipart/form-data) and stored in S3 — see AgentsController.Create.
    public IFormFile? Photo { get; set; }
}

public class AgentDto
{
    public int Id { get; set; }
    // Computed server-side from the caller's own JWT — lets the frontend show an Edit button
    // without ever exposing the agent's internal ApplicationUser id to the public API.
    public bool IsOwnProfile { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public bool IsIndependent { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Specialties { get; set; } = new();
    public int PropertiesCount { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewsCount { get; set; }
}

public class AgentSearchQuery
{
    public string? Name { get; set; }
    public string? Specialty { get; set; }
    public string? Company { get; set; }
    public double? MinRating { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class ContactAgentDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(5000)] public string Message { get; set; } = string.Empty;
}
