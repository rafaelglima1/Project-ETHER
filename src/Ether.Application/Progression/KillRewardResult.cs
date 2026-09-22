using Ether.Domain.Items;

namespace Ether.Application.Progression;

/// <summary>Everything a creature kill granted to the killer.</summary>
public sealed record KillRewardResult(
    long ExperienceGained,
    int LevelsGained,
    int Level,
    long Experience,
    long ExperienceToNextLevel,
    IReadOnlyList<ItemInstance> Items);
