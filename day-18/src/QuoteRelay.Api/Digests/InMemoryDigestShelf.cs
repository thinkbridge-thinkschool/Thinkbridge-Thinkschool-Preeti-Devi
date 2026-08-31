using System.Collections.Concurrent;

namespace QuoteRelay.Api.Digests;

/// <summary>Singleton, process-local shelf. Cleared by a restart.</summary>
public sealed class InMemoryDigestShelf : IDigestShelf
{
    private readonly ConcurrentDictionary<Guid, string> _bodies = new();

    public void Stow(Guid assignmentId, string body) => _bodies[assignmentId] = body;

    public bool TryCollect(Guid assignmentId, out string body)
        => _bodies.TryGetValue(assignmentId, out body!);
}
