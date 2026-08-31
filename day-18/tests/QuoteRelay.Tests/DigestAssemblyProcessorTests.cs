using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuoteRelay.Api.Digests;
using QuoteRelay.Api.Relay;
using QuoteRelay.Tests.Support;

namespace QuoteRelay.Tests;

/// <summary>The slow work in isolation, away from the pump.</summary>
public sealed class DigestAssemblyProcessorTests
{
    private static (DigestAssemblyProcessor Processor, InMemoryDigestShelf Shelf) BuildProcessor(
        TimeSpan? slice = null)
    {
        var shelf = new InMemoryDigestShelf();
        var options = Options.Create(new RelayOptions
        {
            RenderSliceDelay = slice ?? TimeSpan.Zero,
        });

        var processor = new DigestAssemblyProcessor(
            new SeededQuoteCatalogue(NullLogger<SeededQuoteCatalogue>.Instance),
            shelf,
            options,
            NullLogger<DigestAssemblyProcessor>.Instance);

        return (processor, shelf);
    }

    [Fact]
    public async Task Every_requested_quote_lands_in_the_finished_digest()
    {
        var (processor, shelf) = BuildProcessor();
        var assignment = RelayTestRig.Mint("reader@example.test", 101, 103);

        await processor.RunAsync(assignment, CancellationToken.None);

        Assert.True(shelf.TryCollect(assignment.AssignmentId, out var body));
        Assert.Contains("Kent Beck", body);
        Assert.Contains("Donald Knuth", body);
        Assert.Contains("reader@example.test", body);
    }

    [Fact]
    public async Task An_id_missing_from_the_catalogue_fails_the_assignment()
    {
        var (processor, _) = BuildProcessor();
        var assignment = RelayTestRig.Mint("reader@example.test", 101, 4242);

        // 4242 is a well-formed id, so request validation lets it through; the
        // failure can only surface here, off the request thread.
        var failure = await Assert.ThrowsAsync<DigestAssemblyException>(
            () => processor.RunAsync(assignment, CancellationToken.None));

        Assert.Contains("4242", failure.Message);
    }

    [Fact]
    public async Task A_failed_assignment_leaves_nothing_on_the_shelf()
    {
        var (processor, shelf) = BuildProcessor();
        var assignment = RelayTestRig.Mint("reader@example.test", 4242);

        await Assert.ThrowsAsync<DigestAssemblyException>(
            () => processor.RunAsync(assignment, CancellationToken.None));

        Assert.False(shelf.TryCollect(assignment.AssignmentId, out _));
    }

    [Fact]
    public async Task Cancellation_stops_assembly_partway_through()
    {
        var (processor, shelf) = BuildProcessor(TimeSpan.FromMilliseconds(200));
        var assignment = RelayTestRig.Mint("reader@example.test", 101, 102, 103, 104, 105);
        using var gate = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => processor.RunAsync(assignment, gate.Token));

        // Nothing was stowed, because the method never reached the shelf.
        Assert.False(shelf.TryCollect(assignment.AssignmentId, out _));
    }
}
