namespace QuoteRelay.Api.Digests;

/// <summary>
/// Raised when an assignment cannot be assembled. Shape-level problems are
/// rejected at the API boundary; this covers the failures that only surface once
/// the work is already off the request thread — an id that is well-formed but
/// absent from the catalogue being the obvious one.
/// </summary>
public sealed class DigestAssemblyException : Exception
{
    public DigestAssemblyException(string message) : base(message)
    {
    }

    public DigestAssemblyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
