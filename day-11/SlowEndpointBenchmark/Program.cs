using Microsoft.EntityFrameworkCore;

const string ConnectionString =
    "Data Source=C:\\Users\\abhinav\\thinkschool\\Thinkbridge-Thinkschool-Preeti-Devi\\day-2\\QuotesApi\\quotes.db";

var options = new DbContextOptionsBuilder<SeedDbContext>()
    .UseSqlite(ConnectionString)
    .Options;

await using var db = new SeedDbContext(options);

var authorCount = await db.Authors.CountAsync();
var quoteCount = await db.Quotes.CountAsync();

Console.WriteLine($"Existing authors: {authorCount:N0}");
Console.WriteLine($"Existing quotes: {quoteCount:N0}");

if (authorCount == 0 && quoteCount == 0)
{
    var authors = Enumerable.Range(1, 100)
        .Select(i => new Author
        {
            Name = $"Author {i}"
        })
        .ToList();

    db.Authors.AddRange(authors);
    await db.SaveChangesAsync();

    var quotes = new List<Quote>(10_000);

    foreach (var author in authors)
    {
        for (var i = 1; i <= 100; i++)
        {
            quotes.Add(new Quote
            {
                AuthorId = author.Id,
                Author = author.Name,
                Text = $"Quote {i} by {author.Name}"
            });
        }
    }

    db.Quotes.AddRange(quotes);
    await db.SaveChangesAsync();

    Console.WriteLine("Seed completed.");
}
else
{
    Console.WriteLine(
        "Seed skipped because the database already contains data.");
}

var finalAuthors = await db.Authors.CountAsync();
var finalQuotes = await db.Quotes.CountAsync();

Console.WriteLine($"Final authors: {finalAuthors:N0}");
Console.WriteLine($"Final quotes: {finalQuotes:N0}");

public class Author
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class Quote
{
    public int Id { get; set; }

    public string Author { get; set; } = string.Empty;

    public int? AuthorId { get; set; }

    public string Text { get; set; } = string.Empty;
}

public class SeedDbContext(DbContextOptions<SeedDbContext> options)
    : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Quote> Quotes => Set<Quote>();
}