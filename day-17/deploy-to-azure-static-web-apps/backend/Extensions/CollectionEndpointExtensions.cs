using System.ComponentModel.DataAnnotations;
using QuotesApi.Models;
using QuotesApi.Models.Dtos;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class CollectionEndpointExtensions
{
    public static IEndpointRouteBuilder MapCollectionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/collections")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateCollectionRequest request,
            ICollectionRepository repository,
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

            try
            {
                var collection = new Collection(
                    request.Name,
                    request.OwnerId);

                var created = await repository.AddAsync(
                    collection,
                    cancellationToken);

                logger.LogInformation(
                    "Created collection {CollectionId} '{Name}' for owner {OwnerId}",
                    created.Id,
                    created.Name,
                    created.OwnerId);

                return Results.Created(
                    $"/api/collections/{created.Id}",
                    CollectionResponse.FromEntity(created));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/{id:int}", async (
            int id,
            ICollectionRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Getting collection {CollectionId}", id);

            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            return collection is null
                ? Results.NotFound()
                : Results.Ok(CollectionResponse.FromEntity(collection));
        });

        group.MapPost("/{id:int}/items", async (
            int id,
            AddItemRequest request,
            ICollectionRepository repository,
            IClock clock,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            try
            {
                collection.AddItem(request.QuoteId, clock.UtcNow);

                await repository.UpdateAsync(
                    collection,
                    cancellationToken);

                logger.LogInformation(
                    "Added quote {QuoteId} to collection {CollectionId}",
                    request.QuoteId,
                    id);

                return Results.Ok(CollectionResponse.FromEntity(collection));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization("can-edit-quotes");

        group.MapDelete("/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ICollectionRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            var collection = await repository.GetByIdAsync(
                id,
                cancellationToken);

            if (collection is null)
                return Results.NotFound();

            try
            {
                collection.RemoveItem(quoteId);

                await repository.UpdateAsync(
                    collection,
                    cancellationToken);

                logger.LogInformation(
                    "Removed quote {QuoteId} from collection {CollectionId}",
                    quoteId,
                    id);

                return Results.Ok(CollectionResponse.FromEntity(collection));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization("can-edit-quotes");

        group.MapDelete("/{id:int}", async (
            int id,
            ICollectionRepository repository,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting collection {CollectionId}", id);

            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        }).RequireAuthorization("can-edit-quotes");

        return endpoints;
    }
}
