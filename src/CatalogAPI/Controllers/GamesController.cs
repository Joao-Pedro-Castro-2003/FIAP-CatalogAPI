using System.ComponentModel.DataAnnotations;
using FiapCloudGames.Contracts;
using CatalogAPI.Data;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CatalogAPI.Controllers; [ApiController, Route("api/games")]
public sealed class GamesController(CatalogDbContext db, IPublishEndpoint bus) : ControllerBase
{
    [Authorize, HttpGet]
    public async Task<IActionResult> List()
    {
        var now = DateTime.UtcNow;
        var games = await db.Games.ToListAsync();
        var promos = await db.Promotions.Where(x => x.Active && x.StartsAt <= now && x.EndsAt >= now).ToListAsync();

        return Ok(games.Select(g =>
        {
            var p = promos.Where(x => x.GameId == g.Id).OrderByDescending(x => x.DiscountPercent).FirstOrDefault();
            return new { g.Id, g.Name, Price = p is null ? g.Price : decimal.Round(g.Price * (100 - p.DiscountPercent) / 100, 2, MidpointRounding.AwayFromZero), PromotionActive = p is not null };
        }));
    }

    [Authorize(Roles = "Admin"), HttpPost]
    public async Task<IActionResult> Create(GameRequest r)
    {
        var g = new Game { Name = r.Name, Price = r.Price };
        db.Add(g);
        await db.SaveChangesAsync();
        return Created($"api/games/{g.Id}", g);
    }

    [Authorize(Roles = "Admin"), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, GameRequest r)
    {
        var g = await db.Games.FindAsync(id);

        if (g is null)
            return NotFound();

        g.Name = r.Name; g.Price = r.Price;

        await db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize(Roles = "Admin"), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var g = await db.Games.FindAsync(id);

        if (g is null)
            return NotFound();

        if (await db.Orders.AnyAsync(x => x.GameId == id) || await db.Library.AnyAsync(x => x.GameId == id))
            return Conflict("Jogo possui compras e nao pode ser removido");
        db.Remove(g);

        await db.SaveChangesAsync();

        return NoContent();
    }

    [Authorize, HttpPost("{id:int}/purchase")]
    public async Task<IActionResult> Purchase(int id)
    {
        var g = await db.Games.FindAsync(id);

        if (g is null)
            return NotFound();

        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (await db.Library.AnyAsync(x => x.UserId == uid && x.GameId == id))
            return Conflict("Jogo ja pertence ao usuario");

        var now = DateTime.UtcNow;
        var promotion = await db.Promotions.Where(p => p.GameId == id && p.Active && p.StartsAt <= now && p.EndsAt >= now).OrderByDescending(p => p.DiscountPercent).FirstOrDefaultAsync();
        var price = promotion is null ? g.Price : decimal.Round(g.Price * (100 - promotion.DiscountPercent) / 100, 2, MidpointRounding.AwayFromZero);
        var o = new Order { UserId = uid, GameId = id, Price = price };

        db.Add(o);

        await db.SaveChangesAsync();

        await bus.Publish(new OrderPlacedEvent(o.Id, o.UserId, o.GameId, o.Price));

        return Accepted(new { o.Id, o.Status }); }
}
public sealed record GameRequest([Required, StringLength(200, MinimumLength = 1)] string Name, [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Price);
