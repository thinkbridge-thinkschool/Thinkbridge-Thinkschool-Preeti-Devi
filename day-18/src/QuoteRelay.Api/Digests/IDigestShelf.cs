namespace QuoteRelay.Api.Digests;

/// <summary>
/// Where finished digests land. The request that submitted the work is long
/// gone by then, so the output needs somewhere to sit until it is collected.
/// </summary>
public interface IDigestShelf
{
    void Stow(Guid assignmentId, string body);

    bool TryCollect(Guid assignmentId, out string body);
}
