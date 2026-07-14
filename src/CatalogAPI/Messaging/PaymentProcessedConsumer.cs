using FiapCloudGames.Contracts;
using CatalogAPI.Data;
using MassTransit;
namespace CatalogAPI.Messaging;

public sealed class PaymentProcessedConsumer(CatalogDbContext db) : IConsumer<PaymentProcessedEvent>
{
    public async Task Consume(ConsumeContext<PaymentProcessedEvent> c)
    {
        var o = await db.Orders.FindAsync(c.Message.OrderId);

        if (o is null) return;

        o.Status = c.Message.Approved ? "Approved" : "Rejected";

        if (c.Message.Approved && !db.Library.Any(x => x.UserId == o.UserId && x.GameId == o.GameId))
        {
            db.Library.Add(new() { UserId = o.UserId, GameId = o.GameId }); 
            
            await db.SaveChangesAsync();
        }
    }
}
