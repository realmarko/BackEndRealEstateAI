using System.ComponentModel.DataAnnotations;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Models.DTOs;

public class InquiryCreateDto
{
    [Required] public Guid ListingId { get; set; }
    [Required, MaxLength(200)] public string SenderName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(320)] public string SenderEmail { get; set; } = string.Empty;
    [MaxLength(30)] public string? SenderPhone { get; set; }
    [Required, MaxLength(5000)] public string Message { get; set; } = string.Empty;
    public FundingMethod? FundingMethod { get; set; }
    public PurchaseTimeline? Timeline { get; set; }
    public bool? HasAgent { get; set; }
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
    public string? FundingMethod { get; set; }
    public string? Timeline { get; set; }
    public bool? HasAgent { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    // True only for the admin-visible notification ListingsController.ContactOwner creates —
    // never set by the regular buyer-inquiry flow (InquiriesController.Create). The owner contact
    // fields below are populated from the listing only when this is true.
    public bool IsOwnerContactNotification { get; set; }
    public string? OwnerContactName { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? OwnerContactEmail { get; set; }
}
