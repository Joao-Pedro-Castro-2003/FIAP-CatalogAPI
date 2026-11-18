using System.ComponentModel.DataAnnotations;
using CatalogAPI.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CatalogAPI.Controllers;

[ApiController, Authorize, Route("search")]
public sealed class SearchController(GameSearchIndex index) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery, Required, StringLength(200)] string q,
        CancellationToken token,
        [FromQuery, Range(1, 100)] int size = 20)
    {
        try
        {
            return Ok(await index.SearchAsync(q.Trim(), size, token));
        }
        catch (HttpRequestException)
        {
            return Problem(statusCode: 503, title: "Busca temporariamente indisponível");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return Problem(statusCode: 503, title: "Tempo limite da busca excedido");
        }
    }
}
