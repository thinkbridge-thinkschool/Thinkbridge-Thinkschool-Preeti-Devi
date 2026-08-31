namespace QuoteRelay.Api.Relay;

/// <summary>
/// One unit of deferred work: assemble a quote digest for a single subscriber.
/// Immutable on purpose — once it is handed to the intake it crosses a thread
/// boundary, so nothing about it may be mutated by the request that created it.
/// </summary>
/// <param name="AssignmentId">Correlation handle the caller polls with.</param>
/// <param name="Subscriber">Where the finished digest is destined for.</param>
/// <param name="QuoteIds">Catalogue ids to pull into the digest, in order.</param>
/// <param name="AcceptedAt">When the API took ownership of the work.</param>
public sealed record RelayAssignment(
    Guid AssignmentId,
    string Subscriber,
    IReadOnlyList<int> QuoteIds,
    DateTimeOffset AcceptedAt);
