using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatalogAPI.Search;

public sealed record GameSearchHit(int Id, string Name, decimal Price);

public sealed class GameSearchIndex(HttpClient client, IConfiguration configuration)
{
    private readonly string index = configuration["Database:Provider"] == "PostgreSQL"
        ? "fcg-games-postgres-v1"
        : "fcg-games-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync(CancellationToken token)
    {
        using var check = new HttpRequestMessage(HttpMethod.Head, index);
        using var existing = await client.SendAsync(check, token);
        if (existing.IsSuccessStatusCode)
            return;
        if (existing.StatusCode != HttpStatusCode.NotFound)
            existing.EnsureSuccessStatusCode();

        using var response = await client.PutAsJsonAsync(index, new
        {
            mappings = new
            {
                properties = new
                {
                    id = new { type = "integer" },
                    name = new { type = "text" },
                    price = new { type = "double" }
                }
            }
        }, token);
        if (response.StatusCode == HttpStatusCode.BadRequest &&
            (await response.Content.ReadAsStringAsync(token)).Contains("resource_already_exists_exception", StringComparison.Ordinal))
            return;
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncAsync(int id, GameSearchHit? game, CancellationToken token)
    {
        var path = $"{index}/_doc/{id}?refresh=wait_for";
        using var response = game is null
            ? await client.DeleteAsync(path, token)
            : await client.PutAsJsonAsync(path, game, token);

        if (game is null && response.StatusCode == HttpStatusCode.NotFound)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (body.RootElement.TryGetProperty("result", out var result) && result.GetString() == "not_found")
                return;
        }
        response.EnsureSuccessStatusCode();
    }

    public async Task<GameSearchHit[]> SearchAsync(string term, int size, CancellationToken token)
    {
        if (client.BaseAddress is null)
            throw new HttpRequestException("Search:Url ausente");

        using var response = await client.PostAsJsonAsync($"{index}/_search", new
        {
            size,
            query = new { match = new { name = new { query = term, fuzziness = "AUTO" } } },
            sort = new[] { "_score", "id" }
        }, token);
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return payload.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray()
            .Select(hit => hit.GetProperty("_source").Deserialize<GameSearchHit>(JsonOptions)!)
            .ToArray();
    }
}
