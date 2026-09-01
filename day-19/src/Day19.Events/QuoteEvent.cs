using System.Text.Json;

namespace Day19.Events;

public sealed record QuoteEvent(Guid EventId, int QuoteId, string EventType);

public static class EventTypes
{
    public const string QuotePublished = "QuotePublished";
    public const string QuoteRetired = "QuoteRetired";

    public const string Unsupported = "UnsupportedEvent";

    public const string TransientProbe = "TransientFailureProbe";

    public static bool IsHandled(string eventType) =>
        eventType is QuotePublished or QuoteRetired;
}

public static class EventCodec
{
    public static readonly JsonSerializerOptions Format = new(JsonSerializerDefaults.Web);

    public static BinaryData Encode(QuoteEvent quoteEvent) =>
        BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(quoteEvent, Format));

    public static bool TryDecode(BinaryData body, out QuoteEvent quoteEvent)
    {
        try
        {
            var decoded = JsonSerializer.Deserialize<QuoteEvent>(body.ToMemory().Span, Format);
            quoteEvent = decoded!;
            return decoded is not null;
        }
        catch (JsonException)
        {
            quoteEvent = null!;
            return false;
        }
    }
}

public sealed class PermanentEventException(string reason, string description)
    : Exception(description)
{
    public string Reason { get; } = reason;
}
