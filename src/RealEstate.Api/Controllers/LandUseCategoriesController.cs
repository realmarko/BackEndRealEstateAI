using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;

namespace RealEstate.Api.Controllers;

// Read-only catalog powering the listing form's "Uso de suelo" dropdown (shown only for Land
// listings — see listing-form.component.ts). Fixed, admin-seeded list (see LandUseCategory's
// HasData seed in ApplicationDbContext), so there's no create/update/delete here.
[ApiController]
[Route("api/land-use-categories")]
public class LandUseCategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public LandUseCategoriesController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<LandUseCategoryDto>>> GetAll()
    {
        var categories = await _db.LandUseCategories
            .OrderBy(c => c.Id)
            .Select(c => new LandUseCategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        return Ok(categories);
    }
}
