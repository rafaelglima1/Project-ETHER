namespace Ether.Application.Abstractions;

/// <summary>
/// Serializes mutations that touch a set of entities so concurrent commands cannot
/// corrupt state (e.g. two attacks against the same target). Locks are acquired in
/// a deterministic order to avoid deadlocks. Process-local in M5.
/// </summary>
public interface IEntityLockProvider
{
    Task<IAsyncDisposable> AcquireAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken);
}
