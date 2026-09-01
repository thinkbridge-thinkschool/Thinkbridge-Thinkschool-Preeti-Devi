namespace Day19.Events;

public sealed class ServiceBusSettings
{
    public const string SectionName = "ServiceBus";

    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    public string TopicName { get; set; } = string.Empty;

    // Must stay empty: the configuration binder appends to an array property
    // rather than replacing it, so a non-empty default would be concatenated
    // with the configured names and start duplicate processors.
    public string[] Subscriptions { get; set; } = [];

    public int WorkersPerSubscription { get; set; } = 2;

    public int MaxConcurrentCalls { get; set; } = 2;

    // Needed on machines running the Azure Connected Machine (Arc) agent, which
    // advertises a managed-identity endpoint that then fails hard instead of
    // reporting itself unavailable, aborting the credential chain before the
    // az login identity is reached.
    public bool ExcludeManagedIdentity { get; set; }

    public TimeSpan ShutdownGrace { get; set; } = TimeSpan.FromSeconds(10);
}
