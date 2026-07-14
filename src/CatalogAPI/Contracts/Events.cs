namespace FiapCloudGames.Contracts; 
public sealed record OrderPlacedEvent(Guid OrderId,int UserId,int GameId,decimal Price);
public sealed record PaymentProcessedEvent(Guid OrderId,int UserId,int GameId,decimal Price,bool Approved,string Reason);
