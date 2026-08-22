namespace QuotesApi.Cqrs.Models;

public sealed record QuoteReadModel(
    int Id,
    string Author,
    string Text);