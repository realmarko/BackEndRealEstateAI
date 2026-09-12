using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class CreateAgentDto
{
    [Required] public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? PhotoUrl { get; set; }
}

public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? PhotoUrl { get; set; }
    public int PropertiesCount { get; set; }
}

public class AgentSearchQuery
{
    public string? Name { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
