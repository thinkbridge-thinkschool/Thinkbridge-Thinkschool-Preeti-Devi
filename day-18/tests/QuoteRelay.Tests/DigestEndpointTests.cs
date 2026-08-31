using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using QuoteRelay.Api.Contracts;
using QuoteRelay.Api.Relay;

namespace QuoteRelay.Tests;

/// <summary>
/// End-to-end over the real host: the point of the exercise is that the caller
/// gets an answer long before the work is done, so that is what gets measured.
/// </summary>
public sealed class DigestEndpointTests : IClassFixture<RelayApiFactory>
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly RelayApiFactory _factory;

    public DigestEndpointTests(RelayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Submitting_a_digest_returns_before_the_digest_exists()
    {
        var client = _factory.CreateClient();
        await client.GetAsync("/relay/vitals"); // warm the host so timing measures the endpoint

        // Five quotes at the configured slice cost roughly two seconds of work.
        var watch = Stopwatch.StartNew();
        var response = await client.PostAsJsonAsync(
            "/relay/digests",
            new DigestSubmission("reader@example.test", [101, 102, 103, 104, 105]));
        watch.Stop();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var receipt = await response.Content.ReadFromJsonAsync<SubmissionReceipt>();
        Assert.NotNull(receipt);
        Assert.NotEqual(Guid.Empty, receipt!.AssignmentId);

        // The caller waited for a queue write, not for five render slices.
        Assert.True(
            watch.Elapsed < TimeSpan.FromMilliseconds(750),
            $"the request path took {watch.ElapsedMilliseconds} ms, which suggests the work did not leave it");

        var settled = await PollUntilSettled(client, receipt.AssignmentId);
        Assert.Equal(nameof(RelayStage.Delivered), settled.Stage);
        Assert.Contains("Kent Beck", settled.Digest!);
    }

    [Fact]
    public async Task A_failing_assignment_is_reported_and_the_next_one_still_runs()
    {
        var client = _factory.CreateClient();

        // 4242 passes shape validation and then fails during assembly.
        var doomed = await Submit(client, "doomed@example.test", [4242]);
        var healthy = await Submit(client, "healthy@example.test", [102]);

        var failed = await PollUntilSettled(client, doomed);
        var delivered = await PollUntilSettled(client, healthy);

        Assert.Equal(nameof(RelayStage.Faulted), failed.Stage);
        Assert.Contains("4242", failed.Note!);
        Assert.Equal(nameof(RelayStage.Delivered), delivered.Stage);
    }

    [Theory]
    [InlineData("", new[] { 101 })]
    [InlineData("not-an-address", new[] { 101 })]
    [InlineData("reader@example.test", new int[0])]
    [InlineData("reader@example.test", new[] { 101, 101 })]
    [InlineData("reader@example.test", new[] { -1 })]
    public async Task Malformed_submissions_are_rejected_on_the_request_thread(string subscriber, int[] quoteIds)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/relay/digests",
            new DigestSubmission(subscriber, quoteIds));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Polling_an_unknown_assignment_returns_not_found()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/relay/digests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> Submit(HttpClient client, string subscriber, int[] quoteIds)
    {
        var response = await client.PostAsJsonAsync(
            "/relay/digests",
            new DigestSubmission(subscriber, quoteIds));

        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<SubmissionReceipt>();
        return receipt!.AssignmentId;
    }

    private static async Task<ProgressView> PollUntilSettled(HttpClient client, Guid assignmentId)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            var view = await client.GetFromJsonAsync<ProgressView>($"/relay/digests/{assignmentId}");

            if (view is not null &&
                view.Stage is not (nameof(RelayStage.Accepted) or nameof(RelayStage.InProgress)))
            {
                return view;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Assignment {assignmentId} never settled within {Patience}.");
    }
}

/// <summary>
/// Hosts the real application, wiring and all. Nothing is substituted: the
/// endpoint, the queue, the pump, the scoped processor and the shutdown path are
/// the ones that ship. The environment is pinned to "Testing" so the
/// Development overrides stay out and the shipped appsettings.json values
/// (a 400 ms render slice, ten quotes per digest) are what the suite measures.
/// </summary>
public sealed class RelayApiFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        return base.CreateHost(builder);
    }
}
