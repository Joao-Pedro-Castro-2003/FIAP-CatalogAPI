namespace CatalogAPI.Data;

public sealed class LibraryEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int GameId { get; set; }
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;
}
