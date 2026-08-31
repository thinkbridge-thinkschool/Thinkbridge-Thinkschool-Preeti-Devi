using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuoteRelay.Api.Relay;

namespace QuoteRelay.Tests.Support;

/// <summary>
/// Builds the relay's real wiring — real channel, real ledger, real pump — with
/// only the processor swapped for a scriptable one. Nothing about the pump under
/// test is mocked, so what these tests exercise is the production code path.
/// </summary>
internal sealed class RelayTestRig : IAsyncDisposable
{
    /// <summary>Upper bound on any signal wait, so a hang fails loudly instead of stalling the suite.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly ServiceProvider _provider;

    private RelayTestRig(ServiceProvider provider)
    {
        _provider = provider;
        Channel = provider.GetRequiredService<BoundedRelayChannel>();
        Ledger = provider.GetRequiredService<WatchfulLedger>();
        Script = provider.GetRequiredService<ProcessorScript>();
        Pump = provider.GetRequiredService<RelayPumpService>();
    }

    public BoundedRelayChannel Channel { get; }

    public WatchfulLedger Ledger { get; }

    public ProcessorScript Script { get; }

    public RelayPumpService Pump { get; }

    public static RelayTestRig Build(int ceiling = 8)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.Configure<RelayOptions>(o =>
        {
            o.Ceiling = ceiling;
            o.RenderSliceDelay = TimeSpan.Zero;
        });

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<BoundedRelayChannel>();
        services.AddSingleton<IRelayIntake>(sp => sp.GetRequiredService<BoundedRelayChannel>());
        services.AddSingleton<IRelayOutlet>(sp => sp.GetRequiredService<BoundedRelayChannel>());

        services.AddSingleton(sp => new WatchfulLedger(
            new InMemoryRelayLedger(sp.GetRequiredService<TimeProvider>())));
        services.AddSingleton<IRelayLedger>(sp => sp.GetRequiredService<WatchfulLedger>());

        services.AddSingleton<ProcessorScript>();
        services.AddScoped<IAssignmentProcessor, ScriptedProcessor>();

        services.AddSingleton<RelayPumpService>();

        return new RelayTestRig(services.BuildServiceProvider(validateScopes: true));
    }

    /// <summary>Starts the pump exactly as the host would.</summary>
    public Task StartPumpAsync() => Pump.StartAsync(CancellationToken.None);

    /// <summary>Stops the pump exactly as the host would: trip the token, then await the loop.</summary>
    public Task StopPumpAsync() => Pump.StopAsync(CancellationToken.None);

    /// <summary>Creates an assignment and offers it to the intake.</summary>
    public (RelayAssignment Assignment, IntakeOutcome Outcome) Offer(string subscriber, params int[] quoteIds)
    {
        var assignment = Mint(subscriber, quoteIds);
        return (assignment, Channel.Offer(assignment));
    }

    /// <summary>Creates an assignment without queueing it.</summary>
    public static RelayAssignment Mint(string subscriber, params int[] quoteIds)
        => new(Guid.NewGuid(), subscriber, quoteIds.Length == 0 ? [101] : quoteIds, DateTimeOffset.UtcNow);

    /// <summary>Awaits a ledger transition, failing the test rather than hanging.</summary>
    public Task AwaitStage(Guid assignmentId, RelayStage stage)
        => Ledger.Reaches(assignmentId, stage).WaitAsync(Patience);

    public async ValueTask DisposeAsync()
    {
        Channel.Seal();

        if (Pump.ExecuteTask is not null)
        {
            try
            {
                await Pump.StopAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // Already torn down by the test.
            }
        }

        Pump.Dispose();
        await _provider.DisposeAsync();
    }
}
