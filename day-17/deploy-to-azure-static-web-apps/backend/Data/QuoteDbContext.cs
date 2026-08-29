using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuoteDbContext(DbContextOptions<QuoteDbContext> options)
    : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Collection>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(80);

            b.OwnsMany(c => c.Items, item =>
            {
                item.WithOwner().HasForeignKey("CollectionId");
                item.Property(i => i.QuoteId);
                item.Property(i => i.AddedAt);
                item.HasKey("CollectionId", nameof(CollectionItem.QuoteId));
            });
        });
    }
}