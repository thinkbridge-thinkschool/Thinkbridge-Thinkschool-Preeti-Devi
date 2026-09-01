using System.Collections.Concurrent;

namespace Day19.Events;

public sealed record LedgerEntry(string Subscription, string MessageId, DateTimeOffset ProcessedAt);

public sealed class ProcessedEventLedger(TimeProvider clock)
{
    private readonly ConcurrentDictionary<(string Subscription, string MessageId), DateTimeOffset> _processed = new();

    public bool AlreadyProcessed(string subscription, string messageId) =>
        _processed.ContainsKey((subscription, messageId));

    // Called only after the handler succeeds: recording earlier would make a
    // failed message look like a duplicate to its own redelivery.
    public void MarkProcessed(string subscription, string messageId) =>
        _processed[(subscription, messageId)] = clock.GetUtcNow();

    public IReadOnlyList<LedgerEntry> Survey() =>
        _processed
            .Select(entry => new LedgerEntry(entry.Key.Subscription, entry.Key.MessageId, entry.Value))
            .OrderBy(entry => entry.ProcessedAt)
            .ToArray();
}
