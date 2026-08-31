namespace QuoteRelay.Api.Relay;

/// <summary>
/// The producer half of the relay, consumed by the HTTP request path.
/// Deliberately synchronous: handing work over must never make the request
/// thread await anything, otherwise we have re-introduced the latency we set
/// out to remove.
/// </summary>
public interface IRelayIntake
{
    /// <summary>Queued assignments not yet picked up by the pump.</summary>
    int Backlog { get; }

    /// <summary>The configured upper bound on <see cref="Backlog"/>.</summary>
    int Ceiling { get; }

    /// <summary>True once <see cref="Seal"/> has run and no further work is admitted.</summary>
    bool IsSealed { get; }

    /// <summary>
    /// Offers work without blocking. Returns <see cref="IntakeOutcome.Saturated"/>
    /// rather than waiting when the queue is full, so the caller can shed load
    /// with a 503 instead of piling up request threads behind a backlog.
    /// </summary>
    IntakeOutcome Offer(RelayAssignment assignment);

    /// <summary>
    /// Refuses all further work and signals the consumer that the queue will
    /// receive no more items. Idempotent.
    /// </summary>
    void Seal();
}
