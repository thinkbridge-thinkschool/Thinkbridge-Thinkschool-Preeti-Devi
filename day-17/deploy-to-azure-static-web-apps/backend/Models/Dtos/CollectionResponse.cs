namespace QuotesApi.Models.Dtos;

public sealed class CollectionResponse
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public List<CollectionItemResponse> Items { get; init; } = [];

    public static CollectionResponse FromEntity(Collection collection) => new()
    {
        Id = collection.Id,
        Name = collection.Name,
        OwnerId = collection.OwnerId,
        Items = collection.Items
            .Select(i => new CollectionItemResponse
            {
                QuoteId = i.QuoteId,
                AddedAt = i.AddedAt
            })
            .ToList()
    };
}

public sealed class CollectionItemResponse
{
    public int QuoteId { get; init; }
    public DateTimeOffset AddedAt { get; init; }
}
