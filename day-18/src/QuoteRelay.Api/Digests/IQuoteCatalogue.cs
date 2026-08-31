namespace QuoteRelay.Api.Digests;

/// <summary>
/// Source of quote rows. Registered scoped, standing in for the EF Core
/// DbContext a real build would use here — which is precisely why the pump has
/// to open a scope per assignment rather than capture one at startup.
/// </summary>
public interface IQuoteCatalogue
{
    Task<QuoteEntry?> LookupAsync(int quoteId, CancellationToken cancellationToken);
}
