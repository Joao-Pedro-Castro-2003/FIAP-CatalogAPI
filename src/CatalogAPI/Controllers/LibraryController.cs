using CatalogAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace CatalogAPI.Controllers;

[Authorize, ApiController, Route("api/library")]
public sealed class LibraryController(CatalogDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Mine()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        return Ok(await (from l in db.Library join g in db.Games on l.GameId equals g.Id where l.UserId == id 
                         select new { g.Id, g.Name, l.AcquiredAt }).ToListAsync());
    }
}
