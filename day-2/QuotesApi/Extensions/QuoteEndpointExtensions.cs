using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Models.Dtos;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class QuoteEndpointExtensions
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/quotes");

        group.MapGet("/", async (
            int? page,
            int? size,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var currentPage = page.GetValueOrDefault(1);
            var pageSize = size.GetValueOrDefault(10);

            if (currentPage < 1 || pageSize is < 1 or > 100)
            {
                var errors = new Dictionary<string, string[]>
                {
                    ["page"] = currentPage < 1
                        ? ["Page must be at least 1."]
                        : [],
                    ["size"] = pageSize is < 1 or > 100
                        ? ["Size must be between 1 and 100."]
                        : []
                };

                return Results.ValidationProblem(errors);
            }

            logger.LogInformation(
                "Getting quotes page {Page} with size {Size}",
                currentPage,
                pageSize);

            var quotes = await repository.GetPagedAsync(
                currentPage,
                pageSize,
                cancellationToken);

            return Results.Ok(quotes);
        });

        group.MapPost("/", async (
            CreateQuoteRequest request,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(request);

            if (!Validator.TryValidateObject(
                    request,
                    validationContext,
                    validationResults,
                    validateAllProperties: true))
            {
                var errors = validationResults
                    .GroupBy(x => x.MemberNames.FirstOrDefault() ?? string.Empty)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(v => v.ErrorMessage ?? "Invalid value.")
                            .ToArray());

                return Results.ValidationProblem(errors);
            }

            var quote = new Quote
            {
                Author = request.Author.Trim(),
                Text = request.Text.Trim()
            };

            var created = await repository.AddAsync(
                quote,
                cancellationToken);

            logger.LogInformation(
                "Created quote {QuoteId} by {Author}",
                created.Id,
                created.Author);

            return Results.Created($"/api/quotes/{created.Id}", created);
        });

        group.MapGet("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Getting quote {QuoteId}", id);

            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return quote is null
                ? Results.NotFound()
                : Results.Ok(quote);
        });

        group.MapDelete("/{id:int}", async (
            int id,
            IQuoteRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting quote {QuoteId}", id);

            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        });

        // Day 11 deliberately slow endpoint.
        //
        // This demonstrates an N+1 query pattern:
        // one query loads all authors, followed by one quote query
        // for each author.
        //
        // With 100 authors, one request produces approximately
        // 101 database queries.
        //
        // The Quotes.AuthorId column intentionally has no database
        // index in the benchmark database.
        group.MapGet("/slow-authors", async (
            QuoteDbContext db,
            CancellationToken cancellationToken) =>
        {
            var authors = await db.Authors
                .AsNoTracking()
                .OrderBy(a => a.Id)
                .ToListAsync(cancellationToken);

            var results = new List<object>(authors.Count);

            foreach (var author in authors)
            {
                var quotes = await db.Quotes
                    .AsNoTracking()
                    .Where(q => q.AuthorId == author.Id)
                    .OrderBy(q => q.Id)
                    .ToListAsync(cancellationToken);

                results.Add(new
                {
                    author.Id,
                    author.Name,
                    QuoteCount = quotes.Count
                });
            }

            return Results.Ok(results);
        });

        return endpoints;
    }
}