using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.Domain.Items;
using Ether.Domain.World;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ether.Infrastructure.Persistence.Converters;

/// <summary>EF Core value converters for domain strongly-typed identifiers.</summary>
internal static class DomainValueConverters
{
    public static readonly ValueConverter<AccountId, Guid> AccountId =
        new(accountId => accountId.Value, value => new AccountId(value));

    public static readonly ValueConverter<CharacterId, Guid> CharacterId =
        new(characterId => characterId.Value, value => new CharacterId(value));

    public static readonly ValueConverter<MapId, int> MapId =
        new(mapId => mapId.Value, value => new MapId(value));

    public static readonly ValueConverter<ItemDefinitionId, string> ItemDefinitionId =
        new(itemDefinitionId => itemDefinitionId.Value, value => new ItemDefinitionId(value));

    public static readonly ValueConverter<ItemInstanceId, Guid> ItemInstanceId =
        new(itemInstanceId => itemInstanceId.Value, value => new ItemInstanceId(value));
}
