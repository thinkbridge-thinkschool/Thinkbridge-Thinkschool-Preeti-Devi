namespace QuoteRelay.Api.Relay;

/// <summary>
/// A deliberately minimal <see cref="IHostedService"/>, kept alongside
/// <see cref="RelayPumpService"/> to make the contrast concrete.
/// </summary>
/// <remarks>
/// This type has no long-running loop at all — it is two lifecycle hooks, which
/// is exactly the shape <c>IHostedService</c> exists for. Its job is to close
/// the intake as soon as shutdown starts, so requests that are still in flight
/// get an honest "not accepting work" answer instead of queueing an assignment
/// the pump will never reach.
/// </remarks>
public sealed class RelayGateSentinel : IHostedService
{
    private readonly IRelayIntake _intake;
    private readonly ILogger<RelayGateSentinel> _logger;

    public RelayGateSentinel(IRelayIntake intake, ILogger<RelayGateSentinel> logger)
    {
        _intake = intake;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        RelayLog.GateOpen(_logger, _intake.Ceiling);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        RelayLog.GateClosing(_logger, _intake.Backlog);
        _intake.Seal();
        return Task.CompletedTask;
    }
}
