using MediatR;
using QuotesApi.Cqrs.Commands;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Cqrs.Handlers;

public sealed class CreateQuoteCommandHandler(
    QuoteDbContext db) : IRequestHandler<CreateQuoteCommand, int>
{
    public async Task<int> Handle(
        CreateQuoteCommand request,
        CancellationToken cancellationToken)
    {
        var author = request.Author.Trim();
        var text = request.Text.Trim();

        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException(
                "Author cannot be empty.",
                nameof(request.Author));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Quote text cannot be empty.",
                nameof(request.Text));
        }

        var quote = new Quote
        {
            Author = author,
            Text = text
        };

        db.Quotes.Add(quote);

        await db.SaveChangesAsync(cancellationToken);

        return quote.Id;
    }
}