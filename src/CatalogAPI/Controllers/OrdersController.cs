using CatalogAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace CatalogAPI.Controllers;
[Authorize, ApiController, Route("api/orders")]
public sealed class OrdersController(CatalogDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null || order.UserId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier)) return NotFound();
        return Ok(order);
    }
}
