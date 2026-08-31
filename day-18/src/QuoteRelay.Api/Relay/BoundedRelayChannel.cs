using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace QuoteRelay.Api.Relay;

/// <summary>
/// Bounded in-process queue backing both halves of the relay. A single
/// <see cref="Channel{T}"/> instance is shared, which is what makes the
/// producer/consumer hand-off allocation-light and lock-free.
/// </summary>
public sealed class BoundedRelayChannel : IRelayIntake, IRelayOutlet
{
    private readonly Channel<RelayAssignment> _lane;
    private int _sealed;

    public BoundedRelayChannel(IOptions<RelayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Ceiling = options.Value.Ceiling;
        ArgumentOutOfRangeException.ThrowIfLessThan(Ceiling, 1);

        // FullMode.Wait combined with TryWrite gives us a non-blocking "is there
        // room?" answer: TryWrite returns false at the ceiling instead of either
        // blocking the request thread or silently dropping an older assignment.
        _lane = Channel.CreateBounded<RelayAssignment>(new BoundedChannelOptions(Ceiling)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public int Ceiling { get; }

    public int Backlog => _lane.Reader.Count;

    public bool IsSealed => Volatile.Read(ref _sealed) == 1;

    public IntakeOutcome Offer(RelayAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        if (IsSealed)
        {
            return IntakeOutcome.Sealed;
        }

        if (_lane.Writer.TryWrite(assignment))
        {
            return IntakeOutcome.Accepted;
        }

        // TryWrite also fails on a completed writer, which races with Seal().
        return IsSealed ? IntakeOutcome.Sealed : IntakeOutcome.Saturated;
    }

    public void Seal()
    {
        if (Interlocked.Exchange(ref _sealed, 1) == 0)
        {
            _lane.Writer.TryComplete();
        }
    }

    public IAsyncEnumerable<RelayAssignment> DrainAsync(CancellationToken stopToken)
        => _lane.Reader.ReadAllAsync(stopToken);
}
