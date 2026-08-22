using MediatR;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Cqrs.Models;
using QuotesApi.Cqrs.Queries;
using QuotesApi.Data;

namespace QuotesApi.Cqrs.Handlers;

public sealed class GetQuoteQueryHandler(
    QuoteDbContext db) : IRequestHandler<GetQuoteQuery, QuoteReadModel?>
{
    public async Task<QuoteReadModel?> Handle(
        GetQuoteQuery request,
        CancellationToken cancellationToken)
    {
        return await db.Quotes
            .AsNoTracking()
            .Where(q => q.Id == request.Id)
            .Select(q => new QuoteReadModel(
                q.Id,
                q.Author,
                q.Text))
            .FirstOrDefaultAsync(cancellationToken);
    }
}