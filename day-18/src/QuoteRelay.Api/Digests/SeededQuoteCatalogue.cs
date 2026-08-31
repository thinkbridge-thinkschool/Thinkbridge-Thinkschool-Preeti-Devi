namespace QuoteRelay.Api.Digests;

/// <summary>
/// Fixed in-memory catalogue. Scoped, and it logs its own instance identity on
/// construction so the log stream shows a distinct instance per assignment —
/// visible proof that <c>CreateScope</c> in the pump is doing something.
/// </summary>
public sealed class SeededQuoteCatalogue : IQuoteCatalogue
{
    private static readonly IReadOnlyDictionary<int, QuoteEntry> Shelf = new Dictionary<int, QuoteEntry>
    {
        [101] = new(101, "Make it work, make it right, make it fast.", "Kent Beck"),
        [102] = new(102, "Simplicity is a great virtue but it requires hard work to achieve it.", "Edsger W. Dijkstra"),
        [103] = new(103, "Premature optimisation is the root of all evil.", "Donald Knuth"),
        [104] = new(104, "Programs must be written for people to read.", "Harold Abelson"),
        [105] = new(105, "Deleted code is debugged code.", "Jeff Sickel"),
    };

    private readonly Guid _instanceTag = Guid.NewGuid();
    private readonly ILogger<SeededQuoteCatalogue> _logger;

    public SeededQuoteCatalogue(ILogger<SeededQuoteCatalogue> logger)
    {
        _logger = logger;
        _logger.LogDebug("Catalogue instance {InstanceTag} created for this scope.", _instanceTag);
    }

    public Task<QuoteEntry?> LookupAsync(int quoteId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Shelf.TryGetValue(quoteId, out var entry) ? entry : null);
    }
}
