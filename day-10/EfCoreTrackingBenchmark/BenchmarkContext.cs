using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EfCoreTrackingBenchmark;

public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    public string Author { get; set; } = "";
}
public class QuoteDto
{
    public int Id { get; set; }
    public string Author { get; set; } = "";
}
public class BenchmarkContext : DbContext
{
    public DbSet<Quote> Quotes => Set<Quote>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite("Data Source=tracking-benchmark.db")
            .EnableSensitiveDataLogging()
            .LogTo(
                Console.WriteLine,
                new[] { DbLoggerCategory.Database.Command.Name },
                LogLevel.Information);
    }
}