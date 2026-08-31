using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuoteRelay.Api.Contracts;
using QuoteRelay.Api.Relay;
using QuoteRelay.Tests.Support;

namespace QuoteRelay.Tests;

/// <summary>
/// Shutdown driven through the real host rather than through the pump alone:
/// <c>IHost.StopAsync</c> is the same call the console lifetime makes when you
/// press Ctrl+C, so this exercises the whole chain - gate sentinel first, then
/// the pump's stopping token, then the token the processor is awaiting on.
/// </summary>
/// <remarks>
/// Its own factory instance, not a shared fixture: the test deliberately tears
/// the host down, so it must not share one with anything else.
/// </remarks>
public sealed class RelayShutdownTests
{
    [Fact]
    public async Task Stopping_the_host_seals_the_intake_and_unwinds_the_pump_cleanly()
    {
        using var factory = new RelayApiFactory();
        var client = factory.CreateClient();

        // Five quotes at a 400 ms slice: comfortably still running when the host
        // is asked to stop a moment from now.
        var response = await client.PostAsJsonAsync(
            "/relay/digests",
            new DigestSubmission("midflight@example.test", [101, 102, 103, 104, 105]));
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<SubmissionReceipt>())!;

        var ledger = factory.Services.GetRequiredService<IRelayLedger>();
        var intake = factory.Services.GetRequiredService<IRelayIntake>();
        var pump = factory.Services.GetRequiredService<RelayPumpService>();

        await WaitUntil(() => ledger.Peek(receipt.AssignmentId)?.Stage == RelayStage.InProgress);

        // The production shutdown path, start to finish.
        await factory.Services.GetRequiredService<IHost>().StopAsync();

        // The sentinel closed the door, so a request arriving during shutdown is
        // told so rather than queueing work nobody will run.
        Assert.True(intake.IsSealed);
        Assert.Equal(IntakeOutcome.Sealed, intake.Offer(RelayTestRig.Mint("late@example.test", 101)));

        // The interrupted assignment is recorded honestly, not reported as done.
        Assert.Equal(RelayStage.Abandoned, ledger.Peek(receipt.AssignmentId)!.Stage);

        // And the loop is finished rather than orphaned: StopAsync only returned
        // because ExecuteAsync had already run to completion.
        Assert.Equal(TaskStatus.RanToCompletion, pump.ExecuteTask!.Status);
        Assert.Equal(1, pump.Abandoned);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("The expected relay state never arrived.");
    }
}
