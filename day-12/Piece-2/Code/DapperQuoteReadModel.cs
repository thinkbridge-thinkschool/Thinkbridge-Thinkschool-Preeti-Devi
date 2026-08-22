namespace QuotesApi.Cqrs.Models;

public sealed class DapperQuoteReadModel
{
    public int Id { get; init; }
    public string Author { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}