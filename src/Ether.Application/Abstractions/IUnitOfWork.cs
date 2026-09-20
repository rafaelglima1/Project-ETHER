namespace Ether.Application.Abstractions;

/// <summary>
/// Transactional boundary for a use case. The unit of work follows the use case,
/// not the individual repository operation.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
