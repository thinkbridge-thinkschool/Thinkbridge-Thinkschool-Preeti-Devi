namespace QuoteRelay.Api.Relay;

/// <summary>
/// The consumer half of the relay. Split from <see cref="IRelayIntake"/> so the
/// pump cannot accidentally enqueue and the API cannot accidentally dequeue.
/// </summary>
public interface IRelayOutlet
{
    /// <summary>
    /// Yields assignments as they arrive, parking asynchronously (no spin, no
    /// polling delay) while the queue is empty. The sequence ends when the
    /// intake is sealed and drained; it throws
    /// <see cref="OperationCanceledException"/> when <paramref name="stopToken"/>
    /// trips while parked.
    /// </summary>
    IAsyncEnumerable<RelayAssignment> DrainAsync(CancellationToken stopToken);
}
