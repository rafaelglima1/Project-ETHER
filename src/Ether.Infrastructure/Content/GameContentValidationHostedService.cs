using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Combat;
using Ether.Domain.Common;
using Ether.Domain.World;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ether.Infrastructure.Content;

/// <summary>Validates the configured starting location before the host accepts work.</summary>
internal sealed class GameContentValidationHostedService : IHostedService
{
    private readonly JsonGameContentCatalog _content;
    private readonly IAbilityCatalog _abilities;
    private readonly CharacterOptions _characterOptions;
    private readonly ILogger<GameContentValidationHostedService> _logger;

    public GameContentValidationHostedService(
        JsonGameContentCatalog content,
        IAbilityCatalog abilities,
        IOptions<CharacterOptions> characterOptions,
        ILogger<GameContentValidationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(characterOptions);

        _content = content;
        _abilities = abilities;
        _characterOptions = characterOptions.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var requiredAbility in new[]
                 {
                     new AbilityId("warrior.basic_attack"),
                     new AbilityId("warrior.power_strike"),
                 })
        {
            if (!_abilities.TryGet(requiredAbility, out _))
            {
                throw new ContentValidationException($"Required Warrior ability '{requiredAbility.Value}' is missing.");
            }
        }

        MapId startingMap;
        WorldPosition startingPosition;
        try
        {
            startingMap = new MapId(_characterOptions.StartingMapId);
            startingPosition = new WorldPosition(startingMap, _characterOptions.StartingX, _characterOptions.StartingY);
        }
        catch (DomainException exception)
        {
            throw new ContentValidationException($"Configured character starting location is invalid: {exception.Message}", exception);
        }

        var map = _content.GetMap(startingMap);
        if (!map.Contains(startingPosition))
        {
            throw new ContentValidationException(
                $"Configured character starting position ({startingPosition.X},{startingPosition.Y}) is outside map '{startingMap.Value}'.");
        }

        _logger.LogInformation(
            "Loaded game content version {ContentVersion}: {MapCount} maps, {ItemCount} items, {AbilityCount} abilities, {CreatureCount} creatures",
            _content.ContentVersion,
            _content.AllMaps.Count,
            _content.AllItems.Count,
            _content.AllAbilities.Count,
            _content.AllCreatures.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
