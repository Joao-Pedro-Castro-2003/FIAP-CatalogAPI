using CatalogAPI.Data;
using CatalogAPI.Search;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Tests;

public sealed class GameSearchQueueTests
{
    [Fact]
    public async Task CapturesExistingGamesAndTracksEachChange()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var game = new Game { Name = "Original", Price = 25 };
        db.Games.Add(game);
        await db.SaveChangesAsync();

        GameIndexWorker.Prepare(db);
        const string sql = "SELECT GameId, Version FROM GameSearchQueue";
        var initial = await db.Database.SqlQueryRaw<GameIndexWorker.PendingGame>(sql).SingleAsync();
        Assert.Equal(game.Id, initial.GameId);

        game.Name = "Updated";
        await db.SaveChangesAsync();
        var updated = await db.Database.SqlQueryRaw<GameIndexWorker.PendingGame>(sql).SingleAsync();
        Assert.NotEqual(initial.Version, updated.Version);

        db.Remove(game);
        await db.SaveChangesAsync();
        var deleted = await db.Database.SqlQueryRaw<GameIndexWorker.PendingGame>(sql).SingleAsync();
        Assert.Equal(game.Id, deleted.GameId);
        Assert.NotEqual(updated.Version, deleted.Version);
    }
}
