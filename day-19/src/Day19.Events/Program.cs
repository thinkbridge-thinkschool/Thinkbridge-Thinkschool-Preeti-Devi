using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Day19.Events;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection(ServiceBusSettings.SectionName).Get<ServiceBusSettings>()
    ?? new ServiceBusSettings();

if (string.IsNullOrWhiteSpace(settings.FullyQualifiedNamespace))
{
    throw new InvalidOperationException(
        "ServiceBus:FullyQualifiedNamespace is not set, e.g. "
        + "--ServiceBus:FullyQualifiedNamespace sb-day19-quotedemo.servicebus.windows.net");
}

// A repeated name would silently start a second set of processors against the
// same subscription.
settings.Subscriptions = settings.Subscriptions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

if (settings.Subscriptions.Length == 0)
{
    throw new InvalidOperationException(
        "ServiceBus:Subscriptions is empty. A topic with no subscriptions drops everything published to it.");
}

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton(_ => new ServiceBusClient(
    settings.FullyQualifiedNamespace,
    new DefaultAzureCredential(new DefaultAzureCredentialOptions
    {
        ExcludeManagedIdentityCredential = settings.ExcludeManagedIdentity,
    }),
    new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpWebSockets }));

builder.Services.AddSingleton<ProcessedEventLedger>();
builder.Services.AddSingleton<EventDispatcher>();
builder.Services.AddSingleton<EventPublisher>();

foreach (var subscription in settings.Subscriptions)
{
    for (var ordinal = 1; ordinal <= settings.WorkersPerSubscription; ordinal++)
    {
        var name = subscription;
        var workerId = $"{subscription}#{ordinal}";

        builder.Services.AddSingleton<IHostedService>(sp => new SubscriptionWorker(
            name,
            workerId,
            sp.GetRequiredService<ServiceBusClient>(),
            sp.GetRequiredService<EventDispatcher>(),
            settings,
            sp.GetRequiredService<ILogger<SubscriptionWorker>>()));
    }
}

// Cleared first: the default builder already added a console provider, and a
// second one prints every line twice.
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(console =>
{
    console.TimestampFormat = "HH:mm:ss.fff ";
    console.SingleLine = true;
});

var app = builder.Build();

app.MapPost("/events", async Task<Results<Ok<PublishReceipt>, ValidationProblem>> (
    PublishRequest request,
    EventPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var faults = EventPublisher.Validate(request, out var quoteEvent);
    if (faults.Count > 0)
    {
        return TypedResults.ValidationProblem(faults);
    }

    return TypedResults.Ok(await publisher.PublishAsync(quoteEvent, cancellationToken));
});

app.MapGet("/state", (EventDispatcher dispatcher, ProcessedEventLedger ledger) => TypedResults.Ok(new
{
    CountBySubscription = dispatcher.CountBySubscription,
    WorkBySubscription = dispatcher.WorkBySubscription,
    Ledger = ledger.Survey(),
}));

// Peek rather than receive, so reading the dead-letter queue does not consume it.
app.MapGet("/dlq", async (ServiceBusClient client, ServiceBusSettings config, CancellationToken cancellationToken) =>
{
    var found = new List<object>();

    foreach (var subscription in config.Subscriptions)
    {
        await using var reader = client.CreateReceiver(
            config.TopicName,
            subscription,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        foreach (var message in await reader.PeekMessagesAsync(50, cancellationToken: cancellationToken))
        {
            found.Add(new
            {
                Subscription = subscription,
                message.MessageId,
                message.CorrelationId,
                message.DeadLetterReason,
                message.DeadLetterErrorDescription,
                message.DeliveryCount,
                message.EnqueuedTime,
                Body = message.Body.ToString(),
            });
        }
    }

    return TypedResults.Ok(found);
});

// Receiving and completing is the only way to clear a dead-letter queue; peeking
// leaves the messages in place.
app.MapDelete("/dlq", async (ServiceBusClient client, ServiceBusSettings config, CancellationToken cancellationToken) =>
{
    var drained = 0;

    foreach (var subscription in config.Subscriptions)
    {
        await using var reader = client.CreateReceiver(
            config.TopicName,
            subscription,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        while (true)
        {
            var batch = await reader.ReceiveMessagesAsync(
                50, TimeSpan.FromSeconds(2), cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var message in batch)
            {
                await reader.CompleteMessageAsync(message, cancellationToken);
                drained++;
            }
        }
    }

    return TypedResults.Ok(new { Drained = drained });
});

app.Run();

public partial class Program;
