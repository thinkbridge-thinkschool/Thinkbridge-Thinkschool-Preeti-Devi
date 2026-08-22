using Dapper;
using MediatR;
using Microsoft.Data.Sqlite;
using QuotesApi.Cqrs.Models;
using QuotesApi.Cqrs.Queries;

namespace QuotesApi.Cqrs.Handlers;

public sealed class GetQuoteWithDapperQueryHandler(
    IConfiguration configuration)
    : IRequestHandler<GetQuoteWithDapperQuery, DapperQuoteReadModel?>
{
    public async Task<DapperQuoteReadModel?> Handle(
        GetQuoteWithDapperQuery request,
        CancellationToken cancellationToken)
    {
        var connectionString =
            configuration.GetConnectionString("Quotes")
            ?? "Data Source=quotes.db";

        await using var connection =
            new SqliteConnection(connectionString);

        const string sql = """
            SELECT
                Id,
                Author,
                Text
            FROM Quotes
            WHERE Id = @Id;
            """;

        return await connection.QuerySingleOrDefaultAsync<DapperQuoteReadModel>(
            new CommandDefinition(
                sql,
                new { request.Id },
                cancellationToken: cancellationToken));
    }
}