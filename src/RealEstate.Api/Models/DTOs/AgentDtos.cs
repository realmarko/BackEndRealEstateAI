using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RealEstate.Api.Models.DTOs;

public class CreateAgentDto
{
    [Required] public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Bio { get; set; }

    // Comma-separated from the form (e.g. "Buyer's Agent, Staging, Relocation") — split in the controller.
    public string? Specialties { get; set; }

    // Uploaded from the device (multipart/form-data) and stored in S3 — see AgentsController.Create.
    public IFormFile? Photo { get; set; }
}

public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Specialties { get; set; } = new();
    public int PropertiesCount { get; set; }
}

public class AgentSearchQuery
{
    public string? Name { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
