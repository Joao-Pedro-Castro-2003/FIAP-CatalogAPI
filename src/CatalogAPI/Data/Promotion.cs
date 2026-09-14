namespace CatalogAPI.Data;

public sealed class Promotion
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int DiscountPercent { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool Active { get; set; }
}
