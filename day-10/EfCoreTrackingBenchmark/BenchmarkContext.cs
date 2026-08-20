using Microsoft.EntityFrameworkCore;

namespace EfCoreTrackingBenchmark;

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string Author { get; set; } = "";
}

public class BenchmarkContext : DbContext
{
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=tracking-benchmark.db");
    }
}