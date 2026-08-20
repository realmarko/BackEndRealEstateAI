using System.ComponentModel.DataAnnotations;

namespace RealEstate.Api.Models.DTOs;

public class InquiryCreateDto
{
    [Required] public Guid ListingId { get; set; }
    [Required] public string SenderName { get; set; } = string.Empty;
    [Required, EmailAddress] public string SenderEmail { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    [Required] public string Message { get; set; } = string.Empty;
}

public class InquiryDto
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
