using Day19.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Day19.Events.Tests;

public sealed class Day19Tests
{
    private const string Worker = "test#1";
    private const string SubA = "sub-a";
    private const string SubB = "sub-b";

    private static EventDispatcher NewDispatcher(out ProcessedEventLedger ledger)
    {
        ledger = new ProcessedEventLedger(TimeProvider.System);
        return new EventDispatcher(ledger, NullLogger<EventDispatcher>.Instance);
    }

    private static BinaryData Body(string eventType, Guid eventId, int quoteId = 101) =>
        EventCodec.Encode(new QuoteEvent(eventId, quoteId, eventType));

    [Fact]
    public void The_event_id_becomes_the_service_bus_message_id()
    {
        var eventId = Guid.NewGuid();

        var message = EventPublisher.BuildMessage(new QuoteEvent(eventId, 101, EventTypes.QuotePublished));

        Assert.Equal(eventId.ToString("D"), message.MessageId);
        Assert.False(string.IsNullOrWhiteSpace(message.CorrelationId));
        Assert.Equal(EventTypes.QuotePublished, message.Subject);
    }

    [Fact]
    public async Task A_repeated_message_id_is_completed_without_being_processed_again()
    {
        var dispatcher = NewDispatcher(out var ledger);
        var eventId = Guid.NewGuid();
        var messageId = eventId.ToString("D");

        var first = await dispatcher.DispatchAsync(
            SubA, Worker, messageId, "c-1",
            Body(EventTypes.QuotePublished, eventId), 1, CancellationToken.None);

        var second = await dispatcher.DispatchAsync(
            SubA, Worker, messageId, "c-1",
            Body(EventTypes.QuotePublished, eventId), 2, CancellationToken.None);

        Assert.Equal(SettlementKind.Complete, first.Kind);

        Assert.Equal(SettlementKind.Complete, second.Kind);
        Assert.Equal("duplicate", second.Reason);

        Assert.Single(dispatcher.WorkBySubscription[SubA]);
        Assert.Single(ledger.Survey());
    }

    [Fact]
    public async Task The_two_subscriptions_do_not_de_duplicate_each_other()
    {
        var dispatcher = NewDispatcher(out _);
        var eventId = Guid.NewGuid();
        var messageId = eventId.ToString("D");

        await dispatcher.DispatchAsync(
            SubA, Worker, messageId, "c-1",
            Body(EventTypes.QuotePublished, eventId), 1, CancellationToken.None);

        await dispatcher.DispatchAsync(
            SubB, Worker, messageId, "c-1",
            Body(EventTypes.QuotePublished, eventId), 1, CancellationToken.None);

        Assert.Single(dispatcher.WorkBySubscription[SubA]);
        Assert.Single(dispatcher.WorkBySubscription[SubB]);
    }

    [Fact]
    public async Task An_unsupported_event_type_is_classified_as_permanent_and_dead_lettered()
    {
        var dispatcher = NewDispatcher(out var ledger);
        var eventId = Guid.NewGuid();

        var settlement = await dispatcher.DispatchAsync(
            SubA, Worker, eventId.ToString("D"), "c-poison",
            Body(EventTypes.Unsupported, eventId), 1, CancellationToken.None);

        Assert.Equal(SettlementKind.DeadLetter, settlement.Kind);
        Assert.Equal("UnsupportedEventType", settlement.Reason);
        Assert.Contains(EventTypes.Unsupported, settlement.Description);

        Assert.Empty(ledger.Survey());
    }

    [Fact]
    public async Task A_body_that_is_not_an_event_is_also_permanent()
    {
        var dispatcher = NewDispatcher(out _);

        var settlement = await dispatcher.DispatchAsync(
            SubA, Worker, "m-malformed", "c-malformed",
            BinaryData.FromString("{\"quoteId\":"), 1, CancellationToken.None);

        Assert.Equal(SettlementKind.DeadLetter, settlement.Kind);
        Assert.Equal("MalformedBody", settlement.Reason);
    }

    [Fact]
    public async Task A_transient_failure_is_abandoned_so_service_bus_redelivers_it()
    {
        var dispatcher = NewDispatcher(out var ledger);
        var eventId = Guid.NewGuid();

        var settlement = await dispatcher.DispatchAsync(
            SubB, Worker, eventId.ToString("D"), "c-transient",
            Body(EventTypes.TransientProbe, eventId), 1, CancellationToken.None);

        Assert.Equal(SettlementKind.Abandon, settlement.Kind);
        Assert.Empty(ledger.Survey());
    }

    [Fact]
    public async Task A_failed_message_is_not_blocked_from_succeeding_on_a_later_delivery()
    {
        var dispatcher = NewDispatcher(out _);
        var eventId = Guid.NewGuid();
        var messageId = eventId.ToString("D");

        await dispatcher.DispatchAsync(
            SubA, Worker, messageId, "c-1",
            Body(EventTypes.TransientProbe, eventId), 1, CancellationToken.None);

        // Same message id, now carrying a payload that works.
        var retry = await dispatcher.DispatchAsync(
            SubA, Worker, messageId, "c-1",
            Body(EventTypes.QuotePublished, eventId), 2, CancellationToken.None);

        Assert.Equal(SettlementKind.Complete, retry.Kind);
        Assert.Single(dispatcher.WorkBySubscription[SubA]);
    }

    [Fact]
    public async Task Cancelling_mid_message_abandons_it_rather_than_losing_it()
    {
        var dispatcher = NewDispatcher(out var ledger);
        var eventId = Guid.NewGuid();

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var settlement = await dispatcher.DispatchAsync(
            SubA, Worker, eventId.ToString("D"), "c-shutdown",
            Body(EventTypes.QuotePublished, eventId), 1, cancelled.Token);

        Assert.Equal(SettlementKind.Abandon, settlement.Kind);
        Assert.Equal("shutdown", settlement.Reason);
        Assert.Empty(ledger.Survey());
        Assert.False(dispatcher.WorkBySubscription.ContainsKey(SubA));
    }
}
