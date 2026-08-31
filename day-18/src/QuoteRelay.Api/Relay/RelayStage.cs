namespace QuoteRelay.Api.Relay;

/// <summary>Lifecycle positions an assignment can occupy in the ledger.</summary>
public enum RelayStage
{
    /// <summary>Handed to the intake; nobody has picked it up yet.</summary>
    Accepted,

    /// <summary>The pump has dequeued it and a processor is running.</summary>
    InProgress,

    /// <summary>Assembled successfully; the body is on the shelf.</summary>
    Delivered,

    /// <summary>The processor threw. The pump survived and moved on.</summary>
    Faulted,

    /// <summary>Shutdown cancelled the work before it finished.</summary>
    Abandoned,
}
