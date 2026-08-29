namespace QuotesApi.Models;

public class Collection
{
    private readonly List<CollectionItem> _items = [];

    private Collection() { } // EF Core

    public Collection(string name, string ownerId)
    {
        SetName(name);
        OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string OwnerId { get; private set; } = string.Empty;
    public IReadOnlyList<CollectionItem> Items => _items.AsReadOnly();

    public void Rename(string newName) => SetName(newName);

    public void AddItem(int quoteId, DateTimeOffset addedAt)
    {
        if (_items.Count >= 50)
            throw new InvalidOperationException(
                "A collection cannot contain more than 50 items.");

        if (_items.Any(i => i.QuoteId == quoteId))
            throw new InvalidOperationException(
                $"Quote {quoteId} is already in this collection.");

        _items.Add(new CollectionItem(quoteId, addedAt));
    }

    public void RemoveItem(int quoteId)
    {
        var item = _items.FirstOrDefault(i => i.QuoteId == quoteId);

        if (item is null)
            throw new InvalidOperationException(
                $"Quote {quoteId} is not in this collection.");

        _items.Remove(item);
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Collection name cannot be empty.", nameof(name));

        if (name.Length < 3 || name.Length > 80)
            throw new ArgumentException(
                "Collection name must be between 3 and 80 characters.",
                nameof(name));

        Name = name.Trim();
    }
}
