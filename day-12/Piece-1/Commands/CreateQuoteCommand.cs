using MediatR;

namespace QuotesApi.Cqrs.Commands;

public sealed record CreateQuoteCommand(
    string Author,
    string Text) : IRequest<int>;