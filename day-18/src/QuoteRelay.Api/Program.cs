using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using QuoteRelay.Api.Contracts;
using QuoteRelay.Api.Digests;
using QuoteRelay.Api.Relay;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<RelayOptions>()
    .Bind(builder.Configuration.GetSection(RelayOptions.SectionName))
    .Validate(o => o.Ceiling > 0, "QuoteRelay:Ceiling must be greater than zero.")
    .Validate(o => o.MaxQuotesPerDigest > 0, "QuoteRelay:MaxQuotesPerDigest must be greater than zero.")
    .Validate(o => o.RenderSliceDelay >= TimeSpan.Zero, "QuoteRelay:RenderSliceDelay cannot be negative.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);

// One channel instance, surfaced through two interfaces so the producer and the
// consumer each see only the half they are allowed to touch.
builder.Services.AddSingleton<BoundedRelayChannel>();
builder.Services.AddSingleton<IRelayIntake>(sp => sp.GetRequiredService<BoundedRelayChannel>());
builder.Services.AddSingleton<IRelayOutlet>(sp => sp.GetRequiredService<BoundedRelayChannel>());

builder.Services.AddSingleton<IRelayLedger, InMemoryRelayLedger>();
builder.Services.AddSingleton<IDigestShelf, InMemoryDigestShelf>();

// Scoped: the pump resolves these inside a per-assignment scope.
builder.Services.AddScoped<IQuoteCatalogue, SeededQuoteCatalogue>();
builder.Services.AddScoped<IAssignmentProcessor, DigestAssemblyProcessor>();

// Registered as a singleton first so the vitals endpoint can read its counters,
// then handed to the host as the hosted service. Two registrations pointing at
// one instance, not two instances.
builder.Services.AddSingleton<RelayPumpService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RelayPumpService>());

// Registered after the pump so that on shutdown it stops first (hosted services
// stop in reverse registration order) and the intake closes before the pump
// unwinds.
builder.Services.AddHostedService<RelayGateSentinel>();

var app = builder.Build();

app.MapPost("/relay/digests", Results<AcceptedAtRoute<SubmissionReceipt>, ValidationProblem, ProblemHttpResult> (
    DigestSubmission submission,
    IRelayIntake intake,
    IRelayLedger ledger,
    IOptions<RelayOptions> options,
    TimeProvider clock) =>
{
    var startedAt = Stopwatch.GetTimestamp();

    // Step 1 - validate on the request thread. Cheap, and a caller deserves a
    // synchronous answer about a malformed request rather than a 202 followed by
    // a silent failure in the background.
    var faults = Inspect(submission, options.Value);
    if (faults.Count > 0)
    {
        return TypedResults.ValidationProblem(faults);
    }

    // Step 2 - build the assignment.
    var assignment = new RelayAssignment(
        Guid.NewGuid(),
        submission.Subscriber!.Trim(),
        submission.QuoteIds!.ToArray(),
        clock.GetUtcNow());

    // Step 3 - hand it over. Stamped Accepted before the offer so a poll can
    // never see an unknown id for work that is already queued.
    ledger.Stamp(assignment, RelayStage.Accepted, "Queued; awaiting the relay pump.");

    switch (intake.Offer(assignment))
    {
        case IntakeOutcome.Saturated:
            ledger.Stamp(assignment, RelayStage.Faulted, "Rejected: the relay queue is at its ceiling.");
            return TypedResults.Problem(
                title: "Relay saturated",
                detail: $"The relay is already holding its maximum of {intake.Ceiling} queued assignments. Retry shortly.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        case IntakeOutcome.Sealed:
            ledger.Stamp(assignment, RelayStage.Faulted, "Rejected: the intake is sealed for shutdown.");
            return TypedResults.Problem(
                title: "Relay closed",
                detail: "The relay intake is sealed because the application is shutting down.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    // Step 4 - return. The digest has not been rendered and will not be until the
    // pump reaches it; the elapsed figure below is the entire cost to the caller.
    var handOff = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    return TypedResults.AcceptedAtRoute(
        new SubmissionReceipt(
            assignment.AssignmentId,
            $"/relay/digests/{assignment.AssignmentId}",
            intake.Backlog,
            intake.Ceiling,
            Math.Round(handOff, 3)),
        "DigestProgress",
        new { assignmentId = assignment.AssignmentId });
})
.WithName("SubmitDigest")
.WithSummary("Queues a digest for background assembly and returns without assembling it.");

app.MapGet("/relay/digests/{assignmentId:guid}", Results<Ok<ProgressView>, NotFound> (
    Guid assignmentId,
    IRelayLedger ledger,
    IDigestShelf shelf) =>
{
    var progress = ledger.Peek(assignmentId);
    if (progress is null)
    {
        return TypedResults.NotFound();
    }

    var body = shelf.TryCollect(assignmentId, out var digest) ? digest : null;

    return TypedResults.Ok(new ProgressView(
        progress.AssignmentId,
        progress.Subscriber,
        progress.Stage.ToString(),
        progress.Note,
        progress.UpdatedAt,
        body));
})
.WithName("DigestProgress")
.WithSummary("Reports where one assignment has got to, and returns the digest once it exists.");

app.MapGet("/relay/digests", (IRelayLedger ledger) => TypedResults.Ok(
        ledger.Entries()
            .Select(p => new ProgressView(
                p.AssignmentId, p.Subscriber, p.Stage.ToString(), p.Note, p.UpdatedAt, null))
            .ToArray()))
    .WithName("DigestLedger")
    .WithSummary("Lists every assignment the ledger knows about, newest first.");

app.MapGet("/relay/vitals", (IRelayIntake intake, RelayPumpService pump) => TypedResults.Ok(
        new RelayVitals(intake.Backlog, intake.Ceiling, intake.IsSealed, pump.Handled, pump.Abandoned)))
    .WithName("RelayVitals")
    .WithSummary("Queue depth, ceiling and pump counters.");

app.Run();

// Shape-level checks only. Anything that needs the catalogue is left to the
// processor, because a catalogue lookup is exactly the kind of work being kept
// off the request thread.
static Dictionary<string, string[]> Inspect(DigestSubmission? submission, RelayOptions options)
{
    var faults = new Dictionary<string, string[]>();

    if (submission is null)
    {
        faults["body"] = ["A submission body is required."];
        return faults;
    }

    if (string.IsNullOrWhiteSpace(submission.Subscriber))
    {
        faults[nameof(submission.Subscriber)] = ["A subscriber address is required."];
    }
    else if (submission.Subscriber.Length > 128)
    {
        faults[nameof(submission.Subscriber)] = ["A subscriber address may not exceed 128 characters."];
    }
    else if (!submission.Subscriber.Contains('@'))
    {
        faults[nameof(submission.Subscriber)] = ["A subscriber address must contain an '@'."];
    }

    if (submission.QuoteIds is null || submission.QuoteIds.Length == 0)
    {
        faults[nameof(submission.QuoteIds)] = ["At least one quote id is required."];
    }
    else if (submission.QuoteIds.Length > options.MaxQuotesPerDigest)
    {
        faults[nameof(submission.QuoteIds)] =
            [$"A digest may hold at most {options.MaxQuotesPerDigest} quotes."];
    }
    else if (submission.QuoteIds.Any(id => id <= 0))
    {
        faults[nameof(submission.QuoteIds)] = ["Quote ids must be positive."];
    }
    else if (submission.QuoteIds.Distinct().Count() != submission.QuoteIds.Length)
    {
        faults[nameof(submission.QuoteIds)] = ["Quote ids must be distinct."];
    }

    return faults;
}

// Top-level statements generate an internal Program class; this widens it so the
// test project can host the real application through WebApplicationFactory.
public partial class Program;
