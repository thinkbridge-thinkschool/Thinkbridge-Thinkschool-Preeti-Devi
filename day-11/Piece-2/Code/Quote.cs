namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; set; }

    public string Author { get; set; } = string.Empty;

    public int? AuthorId { get; set; }

    public Author? AuthorEntity { get; set; }

    public string Text { get; set; } = string.Empty;
}