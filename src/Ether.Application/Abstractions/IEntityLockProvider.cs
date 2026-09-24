namespace Ether.Application.Abstractions;

/// <summary>A lease over the entity locks acquired by an operation.</summary>
public interface IEntityLockLease : IAsyncDisposable
{
    /// <summary>Returns whether this lease protects mutations to the given entity.</summary>
    bool Covers(Guid entityId);
}

/// <summary>
/// Serializes mutations that touch a set of entities so concurrent commands cannot
/// corrupt state (e.g. two attacks against the same target). Locks are acquired in
/// a deterministic order to avoid deadlocks. Process-local in M5. The operation that
/// acquires a lease owns it and may pass it to nested mutation services; a nested
/// service must not reacquire an entity already covered by that lease.
/// </summary>
public interface IEntityLockProvider
{
    Task<IEntityLockLease> AcquireAsync(IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken);
}
