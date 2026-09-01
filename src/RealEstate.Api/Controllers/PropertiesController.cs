using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;
using RealEstate.Api.Services;

namespace RealEstate.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PropertiesController : ControllerBase
{
    private readonly RealEstateDbContext _context;
    private readonly IS3UploadService _s3Service;

    public PropertiesController(RealEstateDbContext context, IS3UploadService s3Service)
    {
        _context = context;
        _s3Service = s3Service;
    }

    [HttpPost]
    public async Task<ActionResult<Property>> CreateProperty(
        [FromForm] CreatePropertyDto dto,
        [FromForm] List<IFormFile> images)
    {
        var amenities = await _context.Amenities
            .Where(a => dto.AmenityIds.Contains(a.Id))
            .ToListAsync();

        var property = new Property
        {
            Title = dto.Title,
            Description = dto.Description,
            Type = dto.Type,
            Operation = dto.Operation,
            Price = dto.Price,
            Currency = "MXN",
            Address = dto.Address,
            Neighborhood = dto.Neighborhood,
            City = dto.City,
            State = dto.State,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaM2 = dto.AreaM2,
            ConstructionM2 = dto.ConstructionM2,
            ParkingSpots = dto.ParkingSpots,
            YearBuilt = dto.YearBuilt,
            AgentId = dto.AgentId,
            Status = PropertyStatus.Available,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Amenities = amenities,
            Images = new List<PropertyImage>()
        };

        _context.Properties.Add(property);
        await _context.SaveChangesAsync();

        var sortOrder = 0;
        foreach (var image in images)
        {
            var url = await _s3Service.UploadFileAsync(image, $"properties/{property.Id}");
            _context.PropertyImages.Add(new PropertyImage
            {
                PropertyId = property.Id,
                Url = url,
                IsPrimary = sortOrder == 0,
                SortOrder = sortOrder++
            });
        }
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(CreateProperty), new { id = property.Id }, property);
    }
}
