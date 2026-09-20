using Ether.Application.Abstractions;
using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Application.Characters;

/// <summary>Use case: place a character into the world and return its state.</summary>
public sealed class EnterWorldHandler
{
    private readonly ICharacterRepository _characters;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public EnterWorldHandler(
        ICharacterRepository characters,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _characters = characters;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<CharacterResponse> HandleAsync(CharacterId characterId, CancellationToken cancellationToken)
    {
        var character = await _characters.GetByIdAsync(characterId, cancellationToken).ConfigureAwait(false)
                        ?? throw new Exceptions.CharacterNotFoundException(characterId);

        character.EnterWorld(_timeProvider.GetUtcNow());

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CharacterMapping.ToResponse(character);
    }
}
