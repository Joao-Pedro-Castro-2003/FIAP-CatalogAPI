using Microsoft.EntityFrameworkCore;

namespace CatalogAPI.Data;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<LibraryEntry> Library => Set<LibraryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>().Property(x => x.Name).IsRequired();
        modelBuilder.Entity<Order>().Property(x => x.Status).IsRequired();
        modelBuilder.Entity<Promotion>().HasOne<Game>().WithMany()
            .HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Order>().HasOne<Game>().WithMany()
            .HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LibraryEntry>().HasOne<Game>().WithMany()
            .HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LibraryEntry>().HasIndex(x => new { x.UserId, x.GameId }).IsUnique();
        // Users belong to UsersAPI: no foreign key across service databases.
    }
}
