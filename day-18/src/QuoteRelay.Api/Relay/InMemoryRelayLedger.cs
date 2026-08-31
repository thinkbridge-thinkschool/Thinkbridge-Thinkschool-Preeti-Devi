using System.Collections.Concurrent;

namespace QuoteRelay.Api.Relay;

/// <summary>
/// Process-local ledger. Singleton, because the writer (the pump, on a worker
/// thread) and the readers (request threads) are different callers that must
/// see the same map. Contents die with the process — see the README's note on
/// where Hangfire's durable storage earns its keep.
/// </summary>
public sealed class InMemoryRelayLedger : IRelayLedger
{
    private readonly ConcurrentDictionary<Guid, RelayProgress> _stamps = new();
    private readonly TimeProvider _clock;

    public InMemoryRelayLedger(TimeProvider clock) => _clock = clock;

    public void Stamp(RelayAssignment assignment, RelayStage stage, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var progress = new RelayProgress(
            assignment.AssignmentId,
            assignment.Subscriber,
            stage,
            note,
            _clock.GetUtcNow());

        _stamps[assignment.AssignmentId] = progress;
    }

    public RelayProgress? Peek(Guid assignmentId)
        => _stamps.TryGetValue(assignmentId, out var progress) ? progress : null;

    public IReadOnlyList<RelayProgress> Entries()
        => _stamps.Values.OrderByDescending(p => p.UpdatedAt).ToArray();
}
