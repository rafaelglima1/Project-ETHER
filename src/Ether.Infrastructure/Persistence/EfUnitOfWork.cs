using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Ether.Infrastructure.Persistence;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly EtherDbContext _context;

    public EfUnitOfWork(EtherDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, CharacterConfiguration.UniqueNameIndex))
        {
            // The database unique constraint is the authority for name uniqueness.
            throw new DuplicateCharacterNameException(PendingCharacterName());
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, AccountConfiguration.UniqueEmailIndex))
        {
            throw new DuplicateEmailException(PendingEmail());
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
        string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);

    private string? PendingCharacterName() =>
        _context.ChangeTracker.Entries<Character>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity.Name)
            .FirstOrDefault();

    private string? PendingEmail() =>
        _context.ChangeTracker.Entries<Account>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity.Email.Value)
            .FirstOrDefault();
}
