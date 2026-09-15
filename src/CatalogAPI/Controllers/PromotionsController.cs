using System.ComponentModel.DataAnnotations;
using CatalogAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogAPI.Controllers; 

[Authorize(Roles = "Admin"), ApiController, Route("api/promotions")] 
public sealed class PromotionsController(CatalogDbContext db) : ControllerBase 
{ 
    [HttpPost] 
    public async Task<IActionResult> Create(PromotionRequest r) 
    { 
        if (r.EndsAt <= r.StartsAt) return BadRequest("Data final deve ser posterior a inicial");
        if (await db.Games.FindAsync(r.GameId) is null) 
            return BadRequest("Jogo inexistente"); 
        var p = new Promotion 
        { 
            GameId = r.GameId, 
            DiscountPercent = r.DiscountPercent, 
            StartsAt = r.StartsAt, 
            EndsAt = r.EndsAt, 
            Active = r.Active 
        }; 
        db.Add(p); 
        await db.SaveChangesAsync(); 
        return Created($"api/promotions/{p.Id}", p); 
    } 
} 
public sealed record PromotionRequest(int GameId, [Range(1, 100)] int DiscountPercent, DateTime StartsAt, DateTime EndsAt, bool Active);

