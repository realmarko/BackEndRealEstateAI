using System.ComponentModel.DataAnnotations;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

// Shared editable surface for both create (POST) and full edit (PUT) — unlike Fraccionamiento,
// there's no separate "raw candidate vs. admin-reviewed" distinction here: every row is admin-
// entered from the start, so one DTO shape covers both.
public class InfrastructureProjectUpsertDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] public InfrastructureProjectType Type { get; set; }
    [Required] public InfrastructureProjectStatus Status { get; set; }
    [Range(-90, 90)] public double Latitude { get; set; }
    [Range(-180, 180)] public double Longitude { get; set; }
    [Range(1, 50000)] public double AffectedRadiusMeters { get; set; } = 2000;
    [Required] public DateTime AnnouncedDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    [Url, MaxLength(2048)] public string? SourceUrl { get; set; }
}

public class InfrastructureProjectListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime AnnouncedDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
}

public class InfrastructureProjectDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AffectedRadiusMeters { get; set; }
    public DateTime AnnouncedDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public string? SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// GET /published shape — no CreatedByUserId/audit fields, same "public DTO stays separate from
// the admin one" reasoning as FraccionamientoPublicListItemDto.
public class InfrastructureProjectPublicDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AffectedRadiusMeters { get; set; }
    public DateTime AnnouncedDate { get; set; }
    public DateTime? ExpectedCompletionDate { get; set; }
    public string? SourceUrl { get; set; }
}
