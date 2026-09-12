using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstate.Api.Data;
using RealEstate.Api.Models.DTOs;
using RealEstate.Api.Models.Entities;

namespace RealEstate.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly RealEstateDbContext _db;

    public AgentsController(RealEstateDbContext db)
    {
        _db = db;
    }

    // GET /api/agents?name=smith
    [HttpGet]
    public async Task<ActionResult<PagedResult<AgentDto>>> Search([FromQuery] AgentSearchQuery q)
    {
        var query = _db.Agents.Include(a => a.Properties).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Name))
            query = query.Where(a => a.Name.ToLower().Contains(q.Name!.ToLower()));

        var totalCount = await query.CountAsync();

        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);

        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => ToDto(a))
            .ToListAsync();

        return Ok(new PagedResult<AgentDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AgentDto>> GetById(int id)
    {
        var agent = await _db.Agents.Include(a => a.Properties).FirstOrDefaultAsync(a => a.Id == id);
        return agent is null ? NotFound() : Ok(ToDto(agent));
    }

    private static AgentDto ToDto(Agent a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Email = a.Email,
        Phone = a.Phone,
        PropertiesCount = a.Properties.Count
    };
}
