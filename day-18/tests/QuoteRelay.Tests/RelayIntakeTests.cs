using Microsoft.Extensions.Options;
using QuoteRelay.Api.Relay;
using QuoteRelay.Tests.Support;

namespace QuoteRelay.Tests;

/// <summary>Behaviour of the bounded queue itself, with no pump attached.</summary>
public sealed class RelayIntakeTests
{
    private static BoundedRelayChannel ChannelWithCeiling(int ceiling)
        => new(Options.Create(new RelayOptions { Ceiling = ceiling }));

    [Fact]
    public void Work_offered_below_the_ceiling_is_accepted()
    {
        var channel = ChannelWithCeiling(3);

        var outcome = channel.Offer(RelayTestRig.Mint("reader@example.test", 101, 102));

        Assert.Equal(IntakeOutcome.Accepted, outcome);
        Assert.Equal(1, channel.Backlog);
    }

    [Fact]
    public void Backlog_counts_every_accepted_assignment()
    {
        var channel = ChannelWithCeiling(4);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(IntakeOutcome.Accepted, channel.Offer(RelayTestRig.Mint("reader@example.test")));
        }

        Assert.Equal(4, channel.Backlog);
        Assert.Equal(4, channel.Ceiling);
    }

    [Fact]
    public void The_ceiling_is_enforced_rather_than_the_queue_growing()
    {
        var channel = ChannelWithCeiling(2);
        channel.Offer(RelayTestRig.Mint("first@example.test"));
        channel.Offer(RelayTestRig.Mint("second@example.test"));

        // Third offer has nowhere to go. It must be refused immediately, not
        // parked - a request thread waiting for queue space is the very stall
        // the relay exists to avoid.
        var outcome = channel.Offer(RelayTestRig.Mint("third@example.test"));

        Assert.Equal(IntakeOutcome.Saturated, outcome);
        Assert.Equal(2, channel.Backlog);
    }

    [Fact]
    public async Task Room_freed_by_the_consumer_makes_the_intake_available_again()
    {
        var channel = ChannelWithCeiling(1);
        channel.Offer(RelayTestRig.Mint("first@example.test"));
        Assert.Equal(IntakeOutcome.Saturated, channel.Offer(RelayTestRig.Mint("second@example.test")));

        await using var enumerator = channel.DrainAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        Assert.Equal(IntakeOutcome.Accepted, channel.Offer(RelayTestRig.Mint("third@example.test")));
    }

    [Fact]
    public void A_sealed_intake_refuses_new_work()
    {
        var channel = ChannelWithCeiling(4);
        channel.Seal();

        var outcome = channel.Offer(RelayTestRig.Mint("late@example.test"));

        Assert.Equal(IntakeOutcome.Sealed, outcome);
        Assert.True(channel.IsSealed);
    }

    [Fact]
    public void Sealing_twice_is_harmless()
    {
        var channel = ChannelWithCeiling(4);

        channel.Seal();
        channel.Seal();

        Assert.True(channel.IsSealed);
    }

    [Fact]
    public async Task Draining_ends_once_a_sealed_intake_is_empty()
    {
        var channel = ChannelWithCeiling(4);
        channel.Offer(RelayTestRig.Mint("reader@example.test"));
        channel.Seal();

        var drained = new List<RelayAssignment>();
        await foreach (var assignment in channel.DrainAsync(CancellationToken.None))
        {
            drained.Add(assignment);
        }

        // Already-queued work is still handed over; the sequence then completes
        // normally instead of parking forever.
        Assert.Single(drained);
    }

    [Fact]
    public async Task Draining_an_empty_intake_parks_instead_of_returning()
    {
        var channel = ChannelWithCeiling(4);
        using var gate = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        await using var enumerator = channel.DrainAsync(gate.Token).GetAsyncEnumerator();

        // No work, no seal: the only way out is the token. That is what lets an
        // idle pump cost nothing while it waits.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
    }

    [Fact]
    public void A_ceiling_below_one_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChannelWithCeiling(0));
    }
}
