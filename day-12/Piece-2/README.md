Day 12 — Piece 2: When to Reach for Dapper



Overview



The same quote-by-ID read was implemented using both EF Core and Dapper.



The EF Core implementation remains the default application read path, while Dapper provides a lower-overhead alternative for comparison.



EF Core Implementation



The EF Core query uses the existing `GetQuoteQueryHandler`:



csharp

return await db.Quotes

&#x20;   .AsNoTracking()

&#x20;   .Where(q => q.Id == request.Id)

&#x20;   .Select(q => new QuoteReadModel(

&#x20;       q.Id,

&#x20;       q.Author,

&#x20;       q.Text))

&#x20;   .FirstOrDefaultAsync(cancellationToken);



Dapper Implementation

&#x20;The Dapper query uses GetQuoteWithDapperQueryHandler:



const string sql = """

&#x20;   SELECT

&#x20;       Id,

&#x20;       Author,

&#x20;       Text

&#x20;   FROM Quotes

&#x20;   WHERE Id = @Id;

&#x20;   """;



return await connection.QuerySingleOrDefaultAsync<DapperQuoteReadModel>(

&#x20;   new CommandDefinition(

&#x20;       sql,

&#x20;       new { request.Id },

&#x20;       cancellationToken: cancellationToken));



SQL Comparison



Both implementations perform the same logical operation:

Retrieve one quote by Id

Return Id, Author, and Text



The exact SQL is documented in:



evidence/sql-comparison.txt



Timing Comparison



The benchmark used:



1,000 EF Core requests

1,000 Dapper requests

Same quote ID

Same API environment

Warm-up request before measurement





| Metric  |  EF Core |   Dapper | Improvement |

| ------- | -------: | -------: | ----------: |

| Average | 1.457 ms | 0.640 ms |      56.08% |

| p95     | 2.112 ms | 0.861 ms |      59.23% |

| p99     | 2.970 ms | 1.112 ms |      62.56% |





Dapper was approximately 2.67× faster in this local benchmark.



The complete benchmark output is stored in:



evidence/timing-comparison.txt



Rule for EF Core vs Dapper



EF Core should remain the default because it provides strong typing, LINQ composition, change tracking, migrations, and maintainability with less handwritten SQL. I would reach for Dapper when a specific read path is proven to be performance-sensitive through measurement and the simpler SQL/materialization model provides a meaningful benefit. Dapper should be an evidence-based optimization for hot read paths rather than the default data-access technology across the application.

