using Ether.Application.Abstractions;
using Ether.Application.Exceptions;
using Ether.Contracts.Characters;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Common;
using Ether.Domain.World;

using Microsoft.Extensions.Options;

namespace Ether.Application.Characters;

/// <summary>Use case: create a character for an existing account.</summary>
public sealed class CreateCharacterHandler
{
    private readonly IAccountRepository _accounts;
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly CharacterOptions _options;

    public CreateCharacterHandler(
        IAccountRepository accounts,
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<CharacterOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _accounts = accounts;
        _characters = characters;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<CharacterResponse> HandleAsync(
        AccountId authenticatedAccountId,
        AccountId accountId,
        CreateCharacterRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (authenticatedAccountId != accountId)
        {
            throw new ForbiddenException("Cannot create a character for another account.");
        }

        var account = await _accounts.GetByIdAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            throw new AccountNotFoundException(accountId);
        }

        var characterClass = ParseClass(request.CharacterClass);
        var position = new WorldPosition(
            new MapId(_options.StartingMapId),
            _options.StartingX,
            _options.StartingY);

        var character = Character.Create(
            accountId,
            request.Name,
            characterClass,
            position,
            _timeProvider.GetUtcNow());

        await _characters.AddAsync(character, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CharacterMapping.ToResponse(character);
    }

    private static CharacterClass ParseClass(string value)
    {
        if (!Enum.TryParse<CharacterClass>(value, ignoreCase: true, out var characterClass) ||
            !Enum.IsDefined(characterClass))
        {
            throw new DomainException($"Character class '{value}' is invalid.");
        }

        return characterClass;
    }
}
