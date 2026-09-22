using Ether.Contracts.Characters;
using Ether.Domain.Characters;

namespace Ether.Application.Characters;

/// <summary>Maps the <see cref="Character"/> aggregate to its transport contract.</summary>
internal static class CharacterMapping
{
    public static CharacterResponse ToResponse(Character character) =>
        new(
            character.Id.Value,
            character.AccountId.Value,
            character.Name,
            character.Class.ToString(),
            character.State.ToString(),
            character.Level,
            character.Experience,
            character.MapId.Value,
            character.PositionX,
            character.PositionY,
            character.CreatedAt,
            character.UpdatedAt,
            character.Health,
            character.MaxHealth,
            ExperienceToNextLevel(character.Level, character.Experience));

    private static long ExperienceToNextLevel(int level, long experience)
    {
        var required = Domain.Progression.ExperienceCurve.ExperienceToAdvanceFrom(level);
        var remaining = required - experience;
        return remaining > 0 ? remaining : 0;
    }
}
