using CatalogAPI.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task CreatesSchemaAndPersistsCatalogAcrossContexts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options;
        await using (var db = new CatalogDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var game = new Game { Name = "Game", Price = 49.90m };
            db.Games.Add(game);
            await db.SaveChangesAsync();
            db.Promotions.Add(new Promotion { GameId = game.Id, DiscountPercent = 10,
                Active = true, StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddDays(1) });
            db.Orders.Add(new Order { GameId = game.Id, UserId = 42, Price = game.Price });
            db.Library.Add(new LibraryEntry { GameId = game.Id, UserId = 42 });
            await db.SaveChangesAsync();
        }
        await using var reopened = new CatalogDbContext(options);
        var order = await reopened.Orders.SingleAsync();
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(49.90m, order.Price);
        Assert.True((await reopened.Promotions.SingleAsync()).Active);
        var entry = await reopened.Library.SingleAsync();
        Assert.NotEqual(default, entry.AcquiredAt);
        Assert.Single(await (from l in reopened.Library join g in reopened.Games on l.GameId equals g.Id
                             where l.UserId == 42 select g.Name).ToListAsync());
        reopened.Library.Add(new LibraryEntry { UserId = entry.UserId, GameId = entry.GameId });
        await Assert.ThrowsAsync<DbUpdateException>(() => reopened.SaveChangesAsync());
    }

    [Fact]
    public async Task RejectsPromotionForMissingGame()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Promotions.Add(new Promotion { GameId = 999 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
