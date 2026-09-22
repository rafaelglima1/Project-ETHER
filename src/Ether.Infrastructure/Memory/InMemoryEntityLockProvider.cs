using System.Collections.Concurrent;

using Ether.Application.Abstractions;

namespace Ether.Infrastructure.Memory;

/// <summary>
/// Process-local keyed lock. Locks are acquired in a deterministic order so two
/// sessions cannot deadlock when they touch the same pair of entities.
/// </summary>
public sealed class InMemoryEntityLockProvider : IEntityLockProvider
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public async Task<IAsyncDisposable> AcquireAsync(
        IReadOnlyCollection<Guid> entityIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entityIds);

        var ordered = entityIds.Distinct().OrderBy(id => id).ToArray();
        var acquired = new List<SemaphoreSlim>(ordered.Length);

        try
        {
            foreach (var id in ordered)
            {
                var gate = _gates.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(gate);
            }

            return new Releaser(acquired);
        }
        catch
        {
            ReleaseAll(acquired);
            throw;
        }
    }

    private static void ReleaseAll(List<SemaphoreSlim> gates)
    {
        for (var index = gates.Count - 1; index >= 0; index--)
        {
            gates[index].Release();
        }
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly List<SemaphoreSlim> _gates;
        private bool _released;

        public Releaser(List<SemaphoreSlim> gates)
        {
            _gates = gates;
        }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                ReleaseAll(_gates);
            }

            return ValueTask.CompletedTask;
        }
    }
}
