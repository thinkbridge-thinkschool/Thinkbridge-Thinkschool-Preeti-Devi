namespace QuoteRelay.Api.Relay;

/// <summary>
/// The observation surface for work that has left the request path. Without
/// something like this, deferred work is invisible to the caller: the API has
/// already answered 202 and cannot report the eventual outcome any other way.
/// </summary>
public interface IRelayLedger
{
    /// <summary>Stamps the current stage of an assignment, overwriting the previous stamp.</summary>
    void Stamp(RelayAssignment assignment, RelayStage stage, string? note = null);

    /// <summary>Latest stamp for one assignment, or null if the id is unknown.</summary>
    RelayProgress? Peek(Guid assignmentId);

    /// <summary>Every stamp currently held, newest first.</summary>
    IReadOnlyList<RelayProgress> Entries();
}
