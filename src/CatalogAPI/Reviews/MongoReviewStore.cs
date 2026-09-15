using MongoDB.Driver;
namespace CatalogAPI.Reviews;
public interface IReviewStore
{
    Task<ReviewPage> Read(int gameId, int page, CancellationToken ct);
    Task Save(Review review, CancellationToken ct);
}
public sealed class MongoReviewStore : IReviewStore
{
    private readonly IMongoCollection<Review> reviews;
    private readonly SemaphoreSlim initialization = new(1, 1);
    private bool indexed;
    public MongoReviewStore(IMongoClient client, IConfiguration config)
    {
        reviews = client.GetDatabase(config["Mongo:Database"] ?? "fcg_catalog").GetCollection<Review>("reviews");

    }
    public async Task<ReviewPage> Read(int gameId, int page, CancellationToken ct)
    {
        await EnsureIndexes(ct);
        var items = await reviews.Find(x => x.GameId == gameId)
            .SortByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * 20).Limit(20).ToListAsync(ct);
        return new(items, page, 20);
    }
    private async Task EnsureIndexes(CancellationToken ct)
    {
        if (indexed) return;
        await initialization.WaitAsync(ct);
        try {
            if (!indexed) {
                await reviews.Indexes.CreateOneAsync(new CreateIndexModel<Review>(
                    Builders<Review>.IndexKeys.Ascending(x => x.GameId).Descending(x => x.UpdatedAt)), cancellationToken: ct);
                indexed = true;
            }
        } finally { initialization.Release(); }
    }
    public async Task Save(Review review, CancellationToken ct)
    {
        // _id ensures one review per user/game.
        await reviews.ReplaceOneAsync(x => x.Id == review.Id, review, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
