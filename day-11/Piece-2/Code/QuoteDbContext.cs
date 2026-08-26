using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

public class QuoteDbContext(DbContextOptions<QuoteDbContext> options)
    : DbContext(options)
{
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Collection> Collections => Set<Collection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(b =>
        {
            b.Property(a => a.Name)
                .HasMaxLength(200)
                .IsRequired();
        });

        modelBuilder.Entity<Quote>(b =>
        {
            b.HasOne(q => q.AuthorEntity)
                .WithMany(a => a.Quotes)
                .HasForeignKey(q => q.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Day 11 optimization:
            // Add an index because queries filter Quotes by AuthorId.
            b.HasIndex(q => q.AuthorId);
        });

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