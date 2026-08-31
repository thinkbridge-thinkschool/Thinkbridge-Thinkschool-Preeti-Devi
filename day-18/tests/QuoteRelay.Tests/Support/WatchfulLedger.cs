using System.Collections.Concurrent;
using QuoteRelay.Api.Relay;

namespace QuoteRelay.Tests.Support;

/// <summary>
/// Wraps a real ledger and hands out a task per (assignment, stage) pair, so
/// tests can await the exact transition they care about instead of sleeping and
/// hoping. Every wait in this suite is signal-driven for that reason.
/// </summary>
internal sealed class WatchfulLedger : IRelayLedger
{
    private readonly IRelayLedger _inner;
    private readonly ConcurrentDictionary<(Guid, RelayStage), TaskCompletionSource> _signals = new();
    private readonly ConcurrentQueue<(Guid AssignmentId, RelayStage Stage)> _history = new();

    public WatchfulLedger(IRelayLedger inner) => _inner = inner;

    /// <summary>Every stamp in the order it was written.</summary>
    public IReadOnlyList<(Guid AssignmentId, RelayStage Stage)> History => _history.ToArray();

    /// <summary>Completes when <paramref name="assignmentId"/> reaches <paramref name="stage"/>.</summary>
    public Task Reaches(Guid assignmentId, RelayStage stage)
        => Signal(assignmentId, stage).Task;

    public void Stamp(RelayAssignment assignment, RelayStage stage, string? note = null)
    {
        _inner.Stamp(assignment, stage, note);
        _history.Enqueue((assignment.AssignmentId, stage));
        Signal(assignment.AssignmentId, stage).TrySetResult();
    }

    public RelayProgress? Peek(Guid assignmentId) => _inner.Peek(assignmentId);

    public IReadOnlyList<RelayProgress> Entries() => _inner.Entries();

    private TaskCompletionSource Signal(Guid assignmentId, RelayStage stage)
        => _signals.GetOrAdd(
            (assignmentId, stage),
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
