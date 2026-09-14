using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;
namespace CatalogAPI.Reviews;
public sealed class Review
{
    [BsonId] public string Id { get; set; } = "";
    public int GameId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public string[] Tags { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
public sealed record ReviewRequest([Range(1, 5)] int Rating,
    [Required, StringLength(2000, MinimumLength = 1)] string Comment,
    [MaxLength(10)] string[]? Tags);
public sealed record ReviewPage(IReadOnlyList<Review> Items, int Page, int PageSize);
public sealed record CachedReviews(ReviewPage Data, string Cache);
