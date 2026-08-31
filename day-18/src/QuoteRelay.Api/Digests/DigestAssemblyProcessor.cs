using System.Text;
using Microsoft.Extensions.Options;
using QuoteRelay.Api.Relay;

namespace QuoteRelay.Api.Digests;

/// <summary>
/// The slow work itself, and the reason any of this exists. Assembling a digest
/// costs one render slice per quote, so a ten-quote digest takes seconds — far
/// too long to hold an HTTP connection open for.
/// </summary>
/// <remarks>
/// Scoped. It depends on <see cref="IQuoteCatalogue"/>, which is also scoped, so
/// resolving this outside a scope would throw — the pump's <c>CreateScope</c>
/// call is load-bearing, not decorative.
/// </remarks>
public sealed class DigestAssemblyProcessor : IAssignmentProcessor
{
    private readonly IQuoteCatalogue _catalogue;
    private readonly IDigestShelf _shelf;
    private readonly RelayOptions _options;
    private readonly ILogger<DigestAssemblyProcessor> _logger;

    public DigestAssemblyProcessor(
        IQuoteCatalogue catalogue,
        IDigestShelf shelf,
        IOptions<RelayOptions> options,
        ILogger<DigestAssemblyProcessor> logger)
    {
        _catalogue = catalogue;
        _shelf = shelf;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RunAsync(RelayAssignment assignment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        var page = new StringBuilder()
            .AppendLine($"Quote digest for {assignment.Subscriber}")
            .AppendLine($"Assignment {assignment.AssignmentId}")
            .AppendLine($"Requested {assignment.AcceptedAt:u}")
            .AppendLine();

        var slot = 0;

        foreach (var quoteId in assignment.QuoteIds)
        {
            // The token arrived from BackgroundService.ExecuteAsync and is passed
            // straight down. Every await below is therefore a point at which
            // shutdown can unwind this method, which is what "cancellation-aware
            // work" means in practice.
            var entry = await _catalogue.LookupAsync(quoteId, cancellationToken).ConfigureAwait(false)
                ?? throw new DigestAssemblyException(
                    $"Quote {quoteId} is absent from the catalogue, so assignment {assignment.AssignmentId} cannot be assembled.");

            await Task.Delay(_options.RenderSliceDelay, cancellationToken).ConfigureAwait(false);

            page.AppendLine($"{++slot}. \"{entry.Body}\" — {entry.Attribution}");

            _logger.LogDebug(
                "Rendered quote {QuoteId} into slot {Slot} of assignment {AssignmentId}.",
                quoteId, slot, assignment.AssignmentId);
        }

        _shelf.Stow(assignment.AssignmentId, page.ToString());
    }
}
