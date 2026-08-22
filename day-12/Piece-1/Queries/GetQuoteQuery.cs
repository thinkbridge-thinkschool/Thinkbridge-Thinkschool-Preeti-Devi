using MediatR;
using QuotesApi.Cqrs.Models;

namespace QuotesApi.Cqrs.Queries;

public sealed record GetQuoteQuery(
    int Id) : IRequest<QuoteReadModel?>;