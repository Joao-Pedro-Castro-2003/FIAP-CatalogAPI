namespace CatalogAPI.Data;

public sealed class Game
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
