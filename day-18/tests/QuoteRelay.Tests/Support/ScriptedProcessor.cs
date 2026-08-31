using QuoteRelay.Api.Relay;

namespace QuoteRelay.Tests.Support;

/// <summary>
/// Singleton holding the behaviour the scoped processor should exhibit, so a
/// test can decide per assignment whether the work succeeds, throws, or blocks
/// until cancelled. Also counts how many processor instances were constructed,
/// which is how the suite observes the pump's per-assignment DI scope.
/// </summary>
internal sealed class ProcessorScript
{
    private int _instantiations;

    public Func<RelayAssignment, CancellationToken, Task> Behaviour { get; set; } =
        static (_, _) => Task.CompletedTask;

    /// <summary>Number of <see cref="ScriptedProcessor"/> instances the container has built.</summary>
    public int Instantiations => Volatile.Read(ref _instantiations);

    internal void NoteInstantiation() => Interlocked.Increment(ref _instantiations);
}

/// <summary>
/// Scoped stand-in for the real assembly processor. Registered scoped precisely
/// so that resolving it outside a scope would fail, matching production wiring.
/// </summary>
internal sealed class ScriptedProcessor : IAssignmentProcessor
{
    private readonly ProcessorScript _script;

    public ScriptedProcessor(ProcessorScript script)
    {
        _script = script;
        _script.NoteInstantiation();
    }

    public Task RunAsync(RelayAssignment assignment, CancellationToken cancellationToken)
        => _script.Behaviour(assignment, cancellationToken);
}
