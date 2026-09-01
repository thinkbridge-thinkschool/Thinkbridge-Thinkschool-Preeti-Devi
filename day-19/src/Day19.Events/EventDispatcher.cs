using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Day19.Events;

public enum SettlementKind
{
    Complete,

    Abandon,

    DeadLetter,
}

public readonly record struct Settlement(SettlementKind Kind, string Reason, string Description)
{
    public static Settlement Complete(string reason) => new(SettlementKind.Complete, reason, string.Empty);

    public static Settlement Abandon(string reason) => new(SettlementKind.Abandon, reason, string.Empty);

    public static Settlement DeadLetter(string reason, string description) =>
        new(SettlementKind.DeadLetter, reason, description);
}

public sealed class EventDispatcher(
    ProcessedEventLedger ledger,
    ILogger<EventDispatcher> log)
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _work =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> WorkBySubscription =>
        _work.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> CountBySubscription =>
        _work.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.OrdinalIgnoreCase);

    public async Task<Settlement> DispatchAsync(
        string subscription,
        string workerId,
        string messageId,
        string correlationId,
        BinaryData body,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        if (ledger.AlreadyProcessed(subscription, messageId))
        {
            log.LogInformation(
                "[{Subscription}] worker {WorkerId} duplicate ignored: MessageId={MessageId} "
                + "CorrelationId={CorrelationId} delivery={DeliveryCount}. Completing without reprocessing.",
                subscription, workerId, messageId, correlationId, deliveryCount);

            // Completed rather than abandoned: the effect already happened, so
            // the message must not come back.
            return Settlement.Complete("duplicate");
        }

        if (!EventCodec.TryDecode(body, out var quoteEvent))
        {
            return Quarantine(subscription, workerId, messageId, "MalformedBody", "The message body is not a QuoteEvent.");
        }

        try
        {
            await HandleAsync(subscription, quoteEvent, cancellationToken);
        }
        catch (PermanentEventException permanent)
        {
            return Quarantine(subscription, workerId, messageId, permanent.Reason, permanent.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            log.LogInformation(
                "[{Subscription}] worker {WorkerId} abandoning MessageId={MessageId} because the host is shutting down.",
                subscription, workerId, messageId);

            return Settlement.Abandon("shutdown");
        }
        catch (Exception ex)
        {
            // Unclassified failures are treated as transient; MaxDeliveryCount on
            // the subscription is what eventually dead-letters them.
            log.LogWarning(
                "[{Subscription}] worker {WorkerId} abandoning MessageId={MessageId} after delivery "
                + "{DeliveryCount}: {Error}. Service Bus will redeliver it.",
                subscription, workerId, messageId, deliveryCount, ex.Message);

            return Settlement.Abandon(ex.Message);
        }

        // Recorded before the worker completes the message, so a crash between
        // the two leaves a redelivery that is recognised rather than repeated.
        ledger.MarkProcessed(subscription, messageId);

        log.LogInformation(
            "[{Subscription}] worker {WorkerId} handled MessageId={MessageId} CorrelationId={CorrelationId} "
            + "quoteId={QuoteId} type={EventType} delivery={DeliveryCount}.",
            subscription, workerId, messageId, correlationId, quoteEvent.QuoteId, quoteEvent.EventType, deliveryCount);

        return Settlement.Complete("handled");
    }

    private async Task HandleAsync(string subscription, QuoteEvent quoteEvent, CancellationToken cancellationToken)
    {
        if (quoteEvent.EventType == EventTypes.TransientProbe)
        {
            throw new InvalidOperationException(
                $"Downstream dependency unavailable for quote {quoteEvent.QuoteId} (scripted transient failure).");
        }

        if (!EventTypes.IsHandled(quoteEvent.EventType))
        {
            throw new PermanentEventException(
                "UnsupportedEventType",
                $"Event type '{quoteEvent.EventType}' is not handled by this consumer build. "
                + $"Supported types: {EventTypes.QuotePublished}, {EventTypes.QuoteRetired}.");
        }

        await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);

        _work.GetOrAdd(subscription, _ => new ConcurrentQueue<string>())
            .Enqueue($"{quoteEvent.EventType}:{quoteEvent.QuoteId}");
    }

    private Settlement Quarantine(
        string subscription, string workerId, string messageId, string reason, string description)
    {
        log.LogError(
            "[{Subscription}] worker {WorkerId} dead-lettering MessageId={MessageId}. "
            + "Reason={Reason} Description={Description}",
            subscription, workerId, messageId, reason, description);

        return Settlement.DeadLetter(reason, description);
    }
}
