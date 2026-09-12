using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class CreateAgentReviewDto
{
    [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
}

public class AgentReviewDto
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
