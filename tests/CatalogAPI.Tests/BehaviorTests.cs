using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using CatalogAPI.Controllers;
using CatalogAPI.Data;
using CatalogAPI.Messaging;
using CatalogAPI.Reviews;
using FiapCloudGames.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
namespace CatalogAPI.Tests;

public class BehaviorTests
{
    private static CatalogDbContext Database(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseSqlite(connection).Options);
    [Fact]
    public async Task PurchaseUsesSameDiscountAsListing()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = Database(connection);
        db.Database.EnsureCreated();
        db.Games.Add(new Game { Id = 1, Name = "Game", Price = 100m });
        db.Promotions.Add(new Promotion { GameId = 1, Active = true, DiscountPercent = 20,
            StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        var bus = new Mock<IPublishEndpoint>();
        var controller = new GamesController(db, bus.Object) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")], "test"))
            }}
        };
        Assert.IsType<AcceptedResult>(await controller.Purchase(1));
        Assert.Equal(80m, (await db.Orders.SingleAsync()).Price);
        bus.Verify(x => x.Publish(It.Is<OrderPlacedEvent>(e => e.Price == 80m && e.UserId == 42), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData(true, "Approved", 1)]
    [InlineData(false, "Rejected", 0)]
    public async Task PersistsPaymentResultAndIgnoresRepeatedEvents(bool approved, string expected, int entries)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = Database(connection);
        db.Database.EnsureCreated();
        db.Games.Add(new Game { Id = 1, Name = "Game", Price = 10 });
        var order = new Order { UserId = 42, GameId = 1, Price = 10 };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var message = new PaymentProcessedEvent(order.Id, 42, 1, 10, approved, "test");
        var context = new Mock<ConsumeContext<PaymentProcessedEvent>>();
        context.SetupGet(x => x.Message).Returns(message);
        var consumer = new PaymentProcessedConsumer(db, NullLogger<PaymentProcessedConsumer>.Instance);
        await consumer.Consume(context.Object);
        await consumer.Consume(context.Object);
        db.ChangeTracker.Clear();
        Assert.Equal(expected, (await db.Orders.SingleAsync()).Status);
        Assert.Equal(entries, await db.Library.CountAsync());
    }
    [Fact]
    public async Task ReviewsCacheHitsAndInvalidatesAfterUpdate()
    {
        var store = new FakeReviews();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new ReviewService(store, cache, NullLogger<ReviewService>.Instance);
        await service.Save(1, 42, new ReviewRequest(5, "Great", ["coop"]), default);
        Assert.Equal("MISS", (await service.Read(1, 1, default)).Cache);
        Assert.Equal("HIT", (await service.Read(1, 1, default)).Cache);
        Assert.Equal(1, store.ReadCount);
        await service.Save(1, 42, new ReviewRequest(4, "Updated", []), default);
        var updated = await service.Read(1, 1, default);
        Assert.Equal("MISS", updated.Cache);
        Assert.Equal(4, Assert.Single(updated.Data.Items).Rating);
        Assert.Equal(2, store.ReadCount);
    }
    [Fact]
    public async Task RedisFailureFallsBackToMongoAndDoesNotUndoWrites()
    {
        var store = new FakeReviews();
        var cache = new Mock<IDistributedCache>();
        cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        cache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        var service = new ReviewService(store, cache.Object, NullLogger<ReviewService>.Instance);
        await service.Save(1, 42, new ReviewRequest(5, "Saved", []), default);
        var result = await service.Read(1, 1, default);
        Assert.Equal("BYPASS", result.Cache);
        Assert.Equal("Saved", Assert.Single(result.Data.Items).Comment);
    }
    private sealed class FakeReviews : IReviewStore
    {
        private Review? value;
        public int ReadCount { get; private set; }
        public Task<ReviewPage> Read(int gameId, int page, CancellationToken ct) {
            ReadCount++; return Task.FromResult(new ReviewPage(value is null ? [] : [value], page, 20));
        }
        public Task Save(Review review, CancellationToken ct) { value = review; return Task.CompletedTask; }
    }
}
