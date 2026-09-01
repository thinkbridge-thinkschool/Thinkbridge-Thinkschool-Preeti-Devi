using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Day19.Events;

public sealed record PublishRequest(Guid? EventId, int? QuoteId, string? EventType);

public sealed record PublishReceipt(string MessageId, string CorrelationId, string Topic, string[] FansOutTo);

public sealed class EventPublisher(
    ServiceBusClient client,
    ServiceBusSettings settings,
    ILogger<EventPublisher> log)
{
    private readonly ServiceBusSender _sender = client.CreateSender(settings.TopicName);

    public static Dictionary<string, string[]> Validate(PublishRequest? request, out QuoteEvent quoteEvent)
    {
        quoteEvent = null!;
        var faults = new Dictionary<string, string[]>();

        if (request is null)
        {
            faults["body"] = ["A request body is required."];
            return faults;
        }

        if (request.QuoteId is null or <= 0)
        {
            faults[nameof(request.QuoteId)] = ["A positive quoteId is required."];
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            faults[nameof(request.EventType)] = ["An eventType is required."];
        }

        if (faults.Count == 0)
        {
            quoteEvent = new QuoteEvent(
                request.EventId ?? Guid.NewGuid(),
                request.QuoteId!.Value,
                request.EventType!.Trim());
        }

        return faults;
    }

    public async Task<PublishReceipt> PublishAsync(QuoteEvent quoteEvent, CancellationToken cancellationToken)
    {
        var message = BuildMessage(quoteEvent);

        await _sender.SendMessageAsync(message, cancellationToken);

        log.LogInformation(
            "Published MessageId={MessageId} CorrelationId={CorrelationId} to topic {Topic}.",
            message.MessageId, message.CorrelationId, settings.TopicName);

        return new PublishReceipt(
            message.MessageId,
            message.CorrelationId,
            settings.TopicName,
            settings.Subscriptions);
    }

    public static ServiceBusMessage BuildMessage(QuoteEvent quoteEvent) =>
        new(EventCodec.Encode(quoteEvent))
        {
            // MessageId is the event's own id, which is the idempotency key the
            // consumers de-duplicate on.
            MessageId = quoteEvent.EventId.ToString("D"),
            CorrelationId = $"day19-{quoteEvent.QuoteId}-{quoteEvent.EventId:N}"[..40],
            Subject = quoteEvent.EventType,
            ContentType = "application/json",
            ApplicationProperties =
            {
                ["eventType"] = quoteEvent.EventType,
                ["quoteId"] = quoteEvent.QuoteId,
            },
        };
}
