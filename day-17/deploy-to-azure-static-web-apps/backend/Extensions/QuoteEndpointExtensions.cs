using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            ClaimsPrincipal user,
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
                Text = request.Text.Trim(),
                UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty
            };

            var created = await repository.AddAsync(
                quote,
                cancellationToken);

            logger.LogInformation(
                "Created quote {QuoteId} by {Author}",
                created.Id,
                created.Author);

            return Results.Created($"/api/quotes/{created.Id}", created);
        }).RequireAuthorization("can-edit-quotes");

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
            IAuthorizationService authorizationService,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting quote {QuoteId}", id);

            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (quote is null)
                return Results.NotFound();

            var authResult = await authorizationService.AuthorizeAsync(user, quote, "OwnerOnly");
            if (!authResult.Succeeded)
                return Results.Forbid();

            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        });

        return endpoints;
    }
}