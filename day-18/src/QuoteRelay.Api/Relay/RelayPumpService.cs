using System.Diagnostics;

namespace QuoteRelay.Api.Relay;

/// <summary>
/// The consumer. A single long-lived loop that parks on the outlet, runs one
/// assignment at a time in its own DI scope, and treats a faulted assignment as
/// an event to record rather than a reason to die.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BackgroundService"/> is the right base class here because this is
/// one continuous operation for the lifetime of the host: there is no meaningful
/// "start" step beyond entering the loop, and the framework already gives us the
/// shutdown token plumbing that a raw <c>IHostedService</c> would make us build
/// by hand. Compare <see cref="RelayGateSentinel"/>, which really is just two
/// lifecycle hooks and therefore implements <c>IHostedService</c> directly.
/// </para>
/// <para>
/// The service is registered as a singleton by <c>AddHostedService</c>, so it
/// cannot hold scoped dependencies as fields. It holds an
/// <see cref="IServiceScopeFactory"/> instead and opens a fresh scope per
/// assignment.
/// </para>
/// </remarks>
public sealed class RelayPumpService : BackgroundService
{
    private readonly IRelayOutlet _outlet;
    private readonly IRelayIntake _intake;
    private readonly IRelayLedger _ledger;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RelayPumpService> _logger;

    private int _handled;
    private int _abandoned;

    public RelayPumpService(
        IRelayOutlet outlet,
        IRelayIntake intake,
        IRelayLedger ledger,
        IServiceScopeFactory scopes,
        ILogger<RelayPumpService> logger)
    {
        _outlet = outlet;
        _intake = intake;
        _ledger = ledger;
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Assignments this pump has finished, successfully or not.</summary>
    public int Handled => Volatile.Read(ref _handled);

    /// <summary>Assignments cut short by shutdown.</summary>
    public int Abandoned => Volatile.Read(ref _abandoned);

    /// <param name="stoppingToken">
    /// Supplied and cancelled by the host. <c>StopAsync</c> on the base class
    /// trips it the moment the application begins shutting down, which is the
    /// only cancellation source this service uses — nothing here manufactures a
    /// token that could outlive the host.
    /// </param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RelayLog.PumpOnline(_logger, _intake.Ceiling);

        try
        {
            // ReadAllAsync parks on an awaitable when the queue is empty, so an
            // idle pump costs no thread and no timer. It completes normally once
            // the intake is sealed and empty, and throws once the token trips.
            await foreach (var assignment in _outlet.DrainAsync(stoppingToken).ConfigureAwait(false))
            {
                await HandleAsync(assignment, stoppingToken).ConfigureAwait(false);
            }

            RelayLog.PumpDrained(_logger);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected: this is what a clean shutdown looks like from in here.
            RelayLog.PumpCancelled(_logger);
        }
        finally
        {
            RelayLog.PumpOffline(_logger, Handled, Abandoned);
        }
    }

    private async Task HandleAsync(RelayAssignment assignment, CancellationToken stoppingToken)
    {
        _ledger.Stamp(assignment, RelayStage.InProgress);
        RelayLog.AssignmentPickedUp(
            _logger, assignment.AssignmentId, assignment.Subscriber, assignment.QuoteIds.Count, _intake.Backlog);

        var watch = Stopwatch.StartNew();

        // One scope per assignment, disposed before the next one starts. This is
        // the background equivalent of a request scope: scoped services get the
        // same lifetime guarantees the processor would have inside a controller.
        using var scope = _scopes.CreateScope();

        try
        {
            var processor = scope.ServiceProvider.GetRequiredService<IAssignmentProcessor>();
            await processor.RunAsync(assignment, stoppingToken).ConfigureAwait(false);

            watch.Stop();
            _ledger.Stamp(assignment, RelayStage.Delivered, $"Assembled in {watch.ElapsedMilliseconds} ms.");
            RelayLog.AssignmentDelivered(_logger, assignment.AssignmentId, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown, not a job defect. Record it and let it propagate so the
            // drain loop unwinds instead of picking up another assignment it has
            // no time to finish.
            Interlocked.Increment(ref _abandoned);
            _ledger.Stamp(assignment, RelayStage.Abandoned, "Host shutdown interrupted assembly.");
            RelayLog.AssignmentAbandoned(_logger, assignment.AssignmentId);
            throw;
        }
        catch (Exception ex)
        {
            // A single bad assignment must not take the pump with it. Swallowing
            // it here is deliberate and is paired with a loud log entry plus a
            // Faulted stamp, so the failure is contained but never hidden.
            watch.Stop();
            _ledger.Stamp(assignment, RelayStage.Faulted, ex.Message);
            RelayLog.AssignmentFaulted(_logger, ex, assignment.AssignmentId, watch.ElapsedMilliseconds);
        }
        finally
        {
            Interlocked.Increment(ref _handled);
        }
    }
}
