namespace QuoteRelay.Api.Relay;

/// <summary>
/// The actual slow work. Registered <b>scoped</b>: it is resolved once per
/// assignment from a scope the pump opens, so it may safely depend on scoped
/// collaborators (a DbContext, a per-request-style cache, an HTTP client with
/// per-scope state) exactly as a controller would.
/// </summary>
public interface IAssignmentProcessor
{
    /// <param name="cancellationToken">
    /// The pump forwards the host's shutdown token here unchanged. Anything
    /// genuinely slow inside an implementation must honour it, or shutdown will
    /// stall until the host's timeout expires and the process is torn down.
    /// </param>
    Task RunAsync(RelayAssignment assignment, CancellationToken cancellationToken);
}
