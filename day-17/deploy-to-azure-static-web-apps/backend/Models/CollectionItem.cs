namespace QuotesApi.Models;

public class CollectionItem
{
    private CollectionItem() { } // EF Core

    public CollectionItem(int quoteId, DateTimeOffset addedAt)
    {
        QuoteId = quoteId;
        AddedAt = addedAt;
    }

    public int QuoteId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
}
