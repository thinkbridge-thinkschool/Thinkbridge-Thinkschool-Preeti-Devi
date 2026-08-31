namespace QuoteRelay.Api.Contracts;

/// <summary>Inbound request body for a digest submission.</summary>
public sealed record DigestSubmission(string? Subscriber, int[]? QuoteIds);

/// <summary>
/// What the caller gets back immediately. Note the absence of any digest content:
/// at the moment this is serialised, no quote has been rendered yet.
/// </summary>
public sealed record SubmissionReceipt(
    Guid AssignmentId,
    string StatusUrl,
    int Backlog,
    int Ceiling,
    double HandOffMilliseconds);

/// <summary>Poll response describing where an assignment has got to.</summary>
public sealed record ProgressView(
    Guid AssignmentId,
    string Subscriber,
    string Stage,
    string? Note,
    DateTimeOffset UpdatedAt,
    string? Digest);

/// <summary>Operational snapshot of the relay.</summary>
public sealed record RelayVitals(
    int Backlog,
    int Ceiling,
    bool IntakeSealed,
    int Handled,
    int Abandoned);
