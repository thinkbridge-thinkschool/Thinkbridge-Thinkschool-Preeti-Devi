using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Day19.Events;

public sealed class SubscriptionWorker(
    string subscriptionName,
    string workerId,
    ServiceBusClient client,
    EventDispatcher dispatcher,
    ServiceBusSettings settings,
    ILogger<SubscriptionWorker> log) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(settings.TopicName, subscriptionName, new ServiceBusProcessorOptions
        {
            // Messages are settled by hand; auto-complete would discard the
            // abandon and dead-letter decisions the dispatcher makes.
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentCalls = settings.MaxConcurrentCalls,
        });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        log.LogInformation(
            "Worker {WorkerId} listening on subscription {Subscription} (MaxConcurrentCalls={Concurrency}).",
            workerId, subscriptionName, settings.MaxConcurrentCalls);

        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (stoppingToken.Register(() => parked.TrySetResult()))
        {
            await parked.Task;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_processor is null)
        {
            return;
        }

        // A fresh token, not the caller's: that one is already cancelled, and
        // passing it would drop in-flight work instead of waiting for it.
        using var grace = new CancellationTokenSource(settings.ShutdownGrace);

        try
        {
            await _processor.StopProcessingAsync(grace.Token);
            log.LogInformation("Worker {WorkerId} on {Subscription} stopped cleanly.", workerId, subscriptionName);
        }
        catch (OperationCanceledException)
        {
            log.LogWarning(
                "Worker {WorkerId} on {Subscription} still had work in flight after {Grace}; "
                + "those locks will lapse and Service Bus will redeliver.",
                workerId, subscriptionName, settings.ShutdownGrace);
        }
        finally
        {
            await _processor.DisposeAsync();
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;

        var settlement = await dispatcher.DispatchAsync(
            subscriptionName,
            workerId,
            message.MessageId,
            message.CorrelationId ?? string.Empty,
            message.Body,
            message.DeliveryCount,
            args.CancellationToken);

        switch (settlement.Kind)
        {
            case SettlementKind.Complete:
                await args.CompleteMessageAsync(message, CancellationToken.None);
                break;

            case SettlementKind.DeadLetter:
                await args.DeadLetterMessageAsync(
                    message, settlement.Reason, settlement.Description, CancellationToken.None);
                break;

            default:
                await args.AbandonMessageAsync(message, cancellationToken: CancellationToken.None);
                break;
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        log.LogError(
            args.Exception,
            "Worker {WorkerId} on {Subscription} hit a {Source} error.",
            workerId, subscriptionName, args.ErrorSource);

        return Task.CompletedTask;
    }
}
