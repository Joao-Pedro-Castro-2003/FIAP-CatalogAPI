using CatalogAPI.Data;
using CatalogAPI.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CatalogAPI.Controllers;
[ApiController, Authorize, Route("api/games/{gameId:int}/reviews")]
public sealed class ReviewsController(CatalogDbContext db, ReviewService reviews) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(int gameId, CancellationToken ct, [FromQuery] int page = 1)
    {
        if (page < 1 || page > 1000) return BadRequest("Pagina deve estar entre 1 e 1000");
        if (await db.Games.FindAsync([gameId], ct) is null) return NotFound();
        var result = await reviews.Read(gameId, page, ct);
        Response.Headers["X-Cache"] = result.Cache;
        return Ok(result.Data);
    }
    [HttpPut("me")]
    public async Task<IActionResult> Save(int gameId, ReviewRequest request, CancellationToken ct)
    {
        if (request.Tags?.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 40) == true)
            return BadRequest("Tags devem conter entre 1 e 40 caracteres");
        if (await db.Games.FindAsync([gameId], ct) is null) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        return Ok(await reviews.Save(gameId, userId, request, ct));
    }
}
