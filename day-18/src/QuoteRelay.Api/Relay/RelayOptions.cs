namespace QuoteRelay.Api.Relay;

/// <summary>Tunables for the relay, bound from the "QuoteRelay" configuration section.</summary>
public sealed class RelayOptions
{
    public const string SectionName = "QuoteRelay";

    /// <summary>Hard ceiling on queued-but-unstarted assignments. Keeps memory bounded.</summary>
    public int Ceiling { get; set; } = 32;

    /// <summary>Largest digest a single request may ask for.</summary>
    public int MaxQuotesPerDigest { get; set; } = 10;

    /// <summary>
    /// Stand-in for the genuinely slow part of assembling a digest (rendering,
    /// third-party lookups, mail hand-off). One slice is spent per quote.
    /// </summary>
    public TimeSpan RenderSliceDelay { get; set; } = TimeSpan.FromMilliseconds(400);
}
