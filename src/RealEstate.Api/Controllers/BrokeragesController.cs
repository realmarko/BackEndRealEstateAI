using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/brokerages")]
public class BrokeragesController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public BrokeragesController(RealEstateDbContext db)
    {
        _db = db;
    }

    // GET /api/brokerages?search=comey — powers the agent signup autocomplete.
    [HttpGet]
    public async Task<ActionResult<List<string>>> Search([FromQuery] string? search)
    {
        var query = _db.Brokerages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Name.ToLower().Contains(search!.ToLower()));

        var names = await query
            .OrderBy(b => b.Name)
            .Select(b => b.Name)
            .Take(50)
            .ToListAsync();

        return Ok(names);
    }
}
