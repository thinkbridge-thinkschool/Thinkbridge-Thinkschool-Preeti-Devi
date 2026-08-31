namespace QuoteRelay.Api.Relay;

/// <summary>The latest known state of one assignment, as observed by the ledger.</summary>
public sealed record RelayProgress(
    Guid AssignmentId,
    string Subscriber,
    RelayStage Stage,
    string? Note,
    DateTimeOffset UpdatedAt);
