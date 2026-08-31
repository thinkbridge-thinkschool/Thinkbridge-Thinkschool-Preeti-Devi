namespace QuoteRelay.Api.Relay;

/// <summary>What happened when a request tried to hand work to the intake.</summary>
public enum IntakeOutcome
{
    /// <summary>The assignment is queued and will be pumped.</summary>
    Accepted,

    /// <summary>The bounded queue is at its ceiling. The caller must back off.</summary>
    Saturated,

    /// <summary>The intake has been sealed — the host is shutting down.</summary>
    Sealed,
}
