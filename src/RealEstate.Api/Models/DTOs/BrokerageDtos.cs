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
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? WorkingHours { get; set; }
    public int AgentsCount { get; set; }
    public int ListingsCount { get; set; }
    // True only for an agent whose own profile belongs to this brokerage — gates the "edit my
    // agency" entry point client-side (the real check is still enforced server-side on Update).
    public bool CanEdit { get; set; }
}

// POST /api/brokerages — Admin-only, pins a physical office on the map. Unlike
// UpdateBrokerageProfileDto (an existing agent polishing their already-resolved brokerage's
// profile), this is how a brand-new, location-bearing row gets created in the first place.
public class CreateBrokerageDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public double Latitude { get; set; }
    [Required] public double Longitude { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [EmailAddress, MaxLength(320)] public string? Email { get; set; }
    [MaxLength(200)] public string? WorkingHours { get; set; }
    [MaxLength(2048)] public string? Website { get; set; }

    // Uploaded from the device (multipart/form-data) and stored in S3 — same pattern as
    // UpdateBrokerageProfileDto.Logo.
    public IFormFile? Photo { get; set; }
}

// PUT /api/brokerages/{id}/admin — Admin-only, full-field edit of any brokerage (unlike
// UpdateBrokerageProfileDto, which only a *member* agent can use, and only on the subset of
// fields a brokerage's own agent should be trusted to change). Covers the location/contact
// fields CreateBrokerageDto sets at creation too, so an admin can fix a typo'd phone number or
// relocate a pin without needing a separate "delete and recreate" flow — and can give a
// location to an agent-resolved brokerage (Latitude/Longitude null) that never had one.
public class AdminUpdateBrokerageDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(30)] public string? Phone { get; set; }
    [EmailAddress, MaxLength(320)] public string? Email { get; set; }
    [MaxLength(200)] public string? WorkingHours { get; set; }
    [MaxLength(150)] public string? State { get; set; }
    [MaxLength(150)] public string? City { get; set; }
    [MaxLength(2048)] public string? Website { get; set; }
    [MaxLength(3000)] public string? Description { get; set; }
    [MaxLength(2048)] public string? FacebookUrl { get; set; }
    [MaxLength(2048)] public string? InstagramUrl { get; set; }
    public IFormFile? Logo { get; set; }
}

// GET /api/brokerages/map — lightweight shape for the map's marker layer; full details load via
// GET /api/brokerages/{id} only once a visitor actually clicks a pin.
public class BrokerageMapItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

// POST /api/brokerages/import — same [FromForm]-wrapped-IFormFile pattern as CreateBrokerageDto.Photo.
public class BrokerageImportDto
{
    [Required] public IFormFile File { get; set; } = null!;
}

// POST /api/brokerages/import — result of a bulk CSV upload (/admin/inmobiliarias). Row-level
// errors, not an all-or-nothing failure: the rows that did validate are already created by the
// time this is returned.
public class BrokerageImportResultDto
{
    public int CreatedCount { get; set; }
    public List<BrokerageImportRowErrorDto> Errors { get; set; } = new();
}

public class BrokerageImportRowErrorDto
{
    // 1-based, counting the header row as row 1 — matches what a spreadsheet/text editor shows,
    // so an admin fixing the CSV can jump straight to the right line.
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
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
