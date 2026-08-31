namespace QuoteRelay.Api.Relay;

/// <summary>
/// Source-generated log messages for the relay. Keeping them in one place makes
/// the event ids stable, which matters when the only evidence that deferred work
/// ran is a log line.
/// </summary>
internal static partial class RelayLog
{
    [LoggerMessage(EventId = 1800, Level = LogLevel.Information,
        Message = "Relay pump online; queue ceiling {Ceiling}.")]
    public static partial void PumpOnline(ILogger logger, int ceiling);

    [LoggerMessage(EventId = 1801, Level = LogLevel.Information,
        Message = "Assignment {AssignmentId} picked up for {Subscriber} ({QuoteCount} quotes); backlog now {Backlog}.")]
    public static partial void AssignmentPickedUp(ILogger logger, Guid assignmentId, string subscriber, int quoteCount, int backlog);

    [LoggerMessage(EventId = 1802, Level = LogLevel.Information,
        Message = "Assignment {AssignmentId} delivered in {ElapsedMs} ms.")]
    public static partial void AssignmentDelivered(ILogger logger, Guid assignmentId, long elapsedMs);

    [LoggerMessage(EventId = 1803, Level = LogLevel.Error,
        Message = "Assignment {AssignmentId} faulted after {ElapsedMs} ms; the pump stays up and continues with the next assignment.")]
    public static partial void AssignmentFaulted(ILogger logger, Exception exception, Guid assignmentId, long elapsedMs);

    [LoggerMessage(EventId = 1804, Level = LogLevel.Warning,
        Message = "Assignment {AssignmentId} abandoned mid-flight because the host is shutting down.")]
    public static partial void AssignmentAbandoned(ILogger logger, Guid assignmentId);

    [LoggerMessage(EventId = 1805, Level = LogLevel.Information,
        Message = "Shutdown signalled; the pump stopped waiting for new work.")]
    public static partial void PumpCancelled(ILogger logger);

    [LoggerMessage(EventId = 1806, Level = LogLevel.Information,
        Message = "Intake sealed and drained; the pump has no more work to do.")]
    public static partial void PumpDrained(ILogger logger);

    [LoggerMessage(EventId = 1807, Level = LogLevel.Information,
        Message = "Relay pump offline after handling {Handled} assignment(s); {Abandoned} left unfinished.")]
    public static partial void PumpOffline(ILogger logger, int handled, int abandoned);

    [LoggerMessage(EventId = 1810, Level = LogLevel.Information,
        Message = "Relay gate open: intake accepting up to {Ceiling} queued assignment(s).")]
    public static partial void GateOpen(ILogger logger, int ceiling);

    [LoggerMessage(EventId = 1811, Level = LogLevel.Information,
        Message = "Relay gate closing: intake sealed with {Backlog} assignment(s) still queued.")]
    public static partial void GateClosing(ILogger logger, int backlog);
}
