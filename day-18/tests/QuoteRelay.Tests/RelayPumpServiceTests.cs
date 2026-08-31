using QuoteRelay.Api.Relay;
using QuoteRelay.Tests.Support;

namespace QuoteRelay.Tests;

/// <summary>
/// The consumer's contract: it drains, it isolates failures, and it stops when
/// the host says stop.
/// </summary>
public sealed class RelayPumpServiceTests
{
    [Fact]
    public async Task Queued_work_is_picked_up_and_marked_delivered()
    {
        await using var rig = RelayTestRig.Build();
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Script.Behaviour = (_, _) =>
        {
            ran.TrySetResult();
            return Task.CompletedTask;
        };

        await rig.StartPumpAsync();
        var (assignment, outcome) = rig.Offer("reader@example.test", 101, 102);

        Assert.Equal(IntakeOutcome.Accepted, outcome);
        await ran.Task.WaitAsync(RelayTestRig.Patience);
        await rig.AwaitStage(assignment.AssignmentId, RelayStage.Delivered);

        Assert.Equal(RelayStage.Delivered, rig.Ledger.Peek(assignment.AssignmentId)!.Stage);
    }

    [Fact]
    public async Task Work_queued_before_the_pump_starts_is_still_processed()
    {
        await using var rig = RelayTestRig.Build();

        var (assignment, _) = rig.Offer("early@example.test", 101);
        await rig.StartPumpAsync();

        await rig.AwaitStage(assignment.AssignmentId, RelayStage.Delivered);
    }

    [Fact]
    public async Task An_assignment_that_throws_does_not_take_the_pump_down()
    {
        await using var rig = RelayTestRig.Build();
        rig.Script.Behaviour = (_, _) => throw new InvalidOperationException("the digest source went missing");

        await rig.StartPumpAsync();
        var (assignment, _) = rig.Offer("doomed@example.test", 999);

        await rig.AwaitStage(assignment.AssignmentId, RelayStage.Faulted);

        var progress = rig.Ledger.Peek(assignment.AssignmentId)!;
        Assert.Equal(RelayStage.Faulted, progress.Stage);
        Assert.Contains("digest source went missing", progress.Note);

        // The failure was recorded, not swallowed, and the loop is still running.
        Assert.NotNull(rig.Pump.ExecuteTask);
        Assert.False(rig.Pump.ExecuteTask!.IsCompleted);
    }

    [Fact]
    public async Task The_assignment_after_a_failure_still_runs()
    {
        await using var rig = RelayTestRig.Build();

        var poison = RelayTestRig.Mint("poison@example.test", 999);
        var healthy = RelayTestRig.Mint("healthy@example.test", 101);

        rig.Script.Behaviour = (assignment, _) => assignment.AssignmentId == poison.AssignmentId
            ? throw new DivideByZeroException("assignment A is unprocessable")
            : Task.CompletedTask;

        await rig.StartPumpAsync();

        // A then B, in that order, on the same single-reader queue.
        Assert.Equal(IntakeOutcome.Accepted, rig.Channel.Offer(poison));
        Assert.Equal(IntakeOutcome.Accepted, rig.Channel.Offer(healthy));

        await rig.AwaitStage(poison.AssignmentId, RelayStage.Faulted);
        await rig.AwaitStage(healthy.AssignmentId, RelayStage.Delivered);

        Assert.Equal(RelayStage.Faulted, rig.Ledger.Peek(poison.AssignmentId)!.Stage);
        Assert.Equal(RelayStage.Delivered, rig.Ledger.Peek(healthy.AssignmentId)!.Stage);

        // B was not merely processed - it was processed after A blew up, which is
        // the ordering the requirement is actually about.
        var history = rig.Ledger.History;
        var failedAt = history.ToList().FindIndex(e => e == (poison.AssignmentId, RelayStage.Faulted));
        var deliveredAt = history.ToList().FindIndex(e => e == (healthy.AssignmentId, RelayStage.Delivered));
        Assert.True(failedAt >= 0 && deliveredAt > failedAt);
    }

    [Fact]
    public async Task Each_assignment_gets_its_own_dependency_injection_scope()
    {
        await using var rig = RelayTestRig.Build();
        await rig.StartPumpAsync();

        var first = RelayTestRig.Mint("one@example.test", 101);
        var second = RelayTestRig.Mint("two@example.test", 102);
        rig.Channel.Offer(first);
        rig.Channel.Offer(second);

        await rig.AwaitStage(first.AssignmentId, RelayStage.Delivered);
        await rig.AwaitStage(second.AssignmentId, RelayStage.Delivered);

        // A scoped processor resolved from a reused scope would have been built
        // once; two instances proves a fresh scope per assignment.
        Assert.Equal(2, rig.Script.Instantiations);
    }

    [Fact]
    public async Task Shutdown_ends_an_idle_pump_without_faulting_it()
    {
        await using var rig = RelayTestRig.Build();
        await rig.StartPumpAsync();

        var loop = rig.Pump.ExecuteTask!;
        Assert.False(loop.IsCompleted, "the pump should still be parked on an empty queue");

        await rig.StopPumpAsync();

        Assert.True(loop.IsCompleted);
        Assert.False(loop.IsFaulted);
        Assert.Equal(TaskStatus.RanToCompletion, loop.Status);
    }

    [Fact]
    public async Task Shutdown_cancels_work_in_flight_and_records_it_as_abandoned()
    {
        await using var rig = RelayTestRig.Build();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Cancellation-aware slow work: it observes the token the pump handed it,
        // which is the same token BackgroundService received from the host.
        rig.Script.Behaviour = async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };

        await rig.StartPumpAsync();
        var (assignment, _) = rig.Offer("midflight@example.test", 101);
        await started.Task.WaitAsync(RelayTestRig.Patience);

        await rig.StopPumpAsync();

        Assert.Equal(RelayStage.Abandoned, rig.Ledger.Peek(assignment.AssignmentId)!.Stage);
        Assert.Equal(1, rig.Pump.Abandoned);
        Assert.Equal(TaskStatus.RanToCompletion, rig.Pump.ExecuteTask!.Status);
    }

    [Fact]
    public async Task Shutdown_does_not_leave_the_loop_running()
    {
        await using var rig = RelayTestRig.Build();
        await rig.StartPumpAsync();

        var (assignment, _) = rig.Offer("reader@example.test", 101);
        await rig.AwaitStage(assignment.AssignmentId, RelayStage.Delivered);

        await rig.StopPumpAsync();

        // Nothing is left detached: the single loop the service owns has ended,
        // and the service spawned no other task to outlive it.
        Assert.True(rig.Pump.ExecuteTask!.IsCompleted);
        Assert.Equal(IntakeOutcome.Accepted, rig.Channel.Offer(RelayTestRig.Mint("nobody@example.test")));
        await Task.Delay(100);
        Assert.Equal(1, rig.Pump.Handled);
    }

    [Fact]
    public async Task A_sealed_and_drained_intake_ends_the_loop_on_its_own()
    {
        await using var rig = RelayTestRig.Build();
        await rig.StartPumpAsync();

        var (assignment, _) = rig.Offer("last@example.test", 101);
        await rig.AwaitStage(assignment.AssignmentId, RelayStage.Delivered);

        rig.Channel.Seal();

        await rig.Pump.ExecuteTask!.WaitAsync(RelayTestRig.Patience);
        Assert.Equal(TaskStatus.RanToCompletion, rig.Pump.ExecuteTask!.Status);
    }
}
