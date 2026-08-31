namespace QuoteRelay.Api.Digests;

/// <summary>A single catalogue row pulled into a digest.</summary>
public sealed record QuoteEntry(int QuoteId, string Body, string Attribution);
