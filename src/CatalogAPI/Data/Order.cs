namespace CatalogAPI.Data;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UserId { get; set; }
    public int GameId { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Pending";
}
