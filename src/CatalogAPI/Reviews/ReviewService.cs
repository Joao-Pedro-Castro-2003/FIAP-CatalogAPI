using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Prometheus;
using StackExchange.Redis;
namespace CatalogAPI.Reviews;
public sealed class ReviewService(IReviewStore store, IDistributedCache cache, ILogger<ReviewService> log)
{
    private static readonly Counter Requests = Metrics.CreateCounter("fcg_review_cache_total",
        "Consultas de avaliacoes por resultado de cache.", new CounterConfiguration { LabelNames = ["result"] });
    // Cache only page one. All other pages read MongoDB.
    private static string Key(int gameId) => $"reviews:v1:{gameId}";
    public async Task<CachedReviews> Read(int gameId, int page, CancellationToken ct)
    {
        var available = page == 1;
        if (available) {
            try {
                var json = await cache.GetStringAsync(Key(gameId), ct);
                if (json is not null && JsonSerializer.Deserialize<ReviewPage>(json) is { } hit) {
                    Requests.WithLabels("hit").Inc();
                    return new(hit, "HIT");
                }
            } catch (Exception ex) when (ex is RedisException or TimeoutException or JsonException) {
                available = false;
                log.LogWarning("Cache indisponivel; consultando MongoDB");
            }
        }
        var result = await store.Read(gameId, page, ct);
        if (available) {
            try {
                await cache.SetStringAsync(Key(gameId), JsonSerializer.Serialize(result),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) }, ct);
            } catch (Exception ex) when (ex is RedisException or TimeoutException) { available = false; }
        }
        Requests.WithLabels(available ? "miss" : "bypass").Inc();
        return new(result, available ? "MISS" : "BYPASS");
    }
    public async Task<Review> Save(int gameId, int userId, ReviewRequest request, CancellationToken ct)
    {
        var review = new Review { Id = $"{gameId}:{userId}", GameId = gameId, UserId = userId,
            Rating = request.Rating, Comment = request.Comment.Trim(), Tags = request.Tags ?? [], UpdatedAt = DateTime.UtcNow };
        await store.Save(review, ct);
        try { await cache.RemoveAsync(Key(gameId), ct); }
        catch (Exception ex) when (ex is RedisException or TimeoutException) {
            log.LogWarning("Avaliacao salva; cache antigo expira em ate 30 segundos");
        }
        return review;
    }
}
