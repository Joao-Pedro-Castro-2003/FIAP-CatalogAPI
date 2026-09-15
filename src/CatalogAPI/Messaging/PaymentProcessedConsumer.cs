using FiapCloudGames.Contracts;
using CatalogAPI.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
namespace CatalogAPI.Messaging;
public sealed class PaymentProcessedConsumer(CatalogDbContext db, ILogger<PaymentProcessedConsumer> log) : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> c)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(c.CancellationToken);
        var order = await db.Orders.FindAsync([c.Message.OrderId], c.CancellationToken);
        if (order is null || order.Status != "Pending") return;
        if (order.UserId != c.Message.UserId || order.GameId != c.Message.GameId || order.Price != c.Message.Price)
            throw new InvalidOperationException("Resultado de pagamento incompativel com pedido");
        order.Status = c.Message.Approved ? "Approved" : "Rejected";
        if (c.Message.Approved && !await db.Library.AnyAsync(x => x.UserId == order.UserId && x.GameId == order.GameId, c.CancellationToken))
            db.Library.Add(new LibraryEntry { UserId = order.UserId, GameId = order.GameId });
        await db.SaveChangesAsync(c.CancellationToken);
        await transaction.CommitAsync(c.CancellationToken);
        log.LogInformation("Pedido {OrderId} finalizado: {Status}", order.Id, order.Status);
    }
}
