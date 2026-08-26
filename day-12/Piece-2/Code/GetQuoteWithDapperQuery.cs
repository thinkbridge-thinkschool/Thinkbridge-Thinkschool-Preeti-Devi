using MediatR;
using QuotesApi.Cqrs.Models;

namespace QuotesApi.Cqrs.Queries;

public sealed record GetQuoteWithDapperQuery(
    int Id) : IRequest<DapperQuoteReadModel?>;