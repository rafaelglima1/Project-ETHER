using System.Net.WebSockets;
using System.Text;

using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.Domain.Creatures;
using Ether.Domain.World;
using Ether.GameServer.Protocol;
using Ether.GameServer.Tests.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ether.GameServer.Tests;

/// <summary>
/// Reproduces: inventory is added to DB via loot, but the WebSocket snapshot after
/// reconnect shows an empty inventory.
/// </summary>
public sealed class InventorySnapshotReconnectTests
{
    private static readonly ProtocolSerializer Serializer = new();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var persistence = new InMemoryPersistence();

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAccountRepository>();
                services.RemoveAll<ICharacterRepository>();
                services.RemoveAll<IItemInstanceRepository>();
                services.RemoveAll<IUnitOfWork>();

                services.AddSingleton(persistence);
                services.AddSingleton<IAccountRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<ICharacterRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IItemInstanceRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryPersistence>());
            });
        });
    }

    private static async Task SendAsync(WebSocket socket, string name, object? payload, long seq, CancellationToken ct)
    {
        var envelope = Serializer.Build(ProtocolMessageTypes.Command, name, payload, Guid.NewGuid(), seq);
        await socket.SendAsync(Encoding.UTF8.GetBytes(Serializer.Serialize(envelope)), WebSocketMessageType.Text, true, ct);
    }

    private static async Task<ProtocolEnvelope> ReceiveAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        while (true)
        {
            var r = await socket.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, r.Count);
            if (r.EndOfMessage) break;
        }
        var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        var env = Serializer.TryDeserialize(json, out _);
        while (env is not null && env.RequestId is null)
        {
            using var ms2 = new MemoryStream();
            while (true)
            {
                var r = await socket.ReceiveAsync(buffer, ct);
                ms2.Write(buffer, 0, r.Count);
                if (r.EndOfMessage) break;
            }
            var json2 = Encoding.UTF8.GetString(ms2.GetBuffer(), 0, (int)ms2.Length);
            env = Serializer.TryDeserialize(json2, out _);
        }
        return env!;
    }

    [Fact]
    public async Task Inventory_in_snapshot_survives_reconnect()
    {
        using var factory = CreateFactory();
        using var ct = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var gameToken = tokenService.CreateGameToken(account, character.Value).Token;

        // Seed a creature adjacent to the player and kill it to generate loot.
        var creatureWorld = factory.Services.GetRequiredService<ICreatureWorld>();
        var slime = CreatureCatalog.Get(new CreatureDefinitionId("creature.slime"));
        var creature = new CreatureInstance(
            CreatureInstanceId.New(), slime.Id, new MapId(1),
            new WorldPosition(new MapId(1), 1, 0), slime.MaxHealth);
        creatureWorld.Add(creature);

        // First session: authenticate + enter world + kill creature.
        using (var socket = await factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/game"), ct.Token))
        {
            await SendAsync(socket, ProtocolMessageNames.GameAuthenticate,
                new AuthenticateCommandPayload(gameToken), 1, ct.Token);
            _ = await ReceiveAsync(socket, ct.Token);

            await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, ct.Token);
            var snap1 = await ReceiveAsync(socket, ct.Token);
            Assert.Equal(ProtocolMessageNames.WorldSnapshot, snap1.Name);
        }

        // Simulate loot being added to inventory directly (as combat would do).
        var itemDefinition = Ether.Domain.Items.ItemCatalog.Get(Ether.Domain.Items.ItemCatalog.SlimeGel);
        var item = Ether.Domain.Items.ItemInstance.CreateLoot(itemDefinition, 2, new CharacterId(character.Value));
        await persistence.AddAsync(item, ct.Token);
        await persistence.SaveChangesAsync(ct.Token);

        // Verify HTTP-style inventory has the item.
        var items = await persistence.GetByOwnerAsync(new CharacterId(character.Value), ct.Token);
        Assert.Single(items);

        // Second session: reconnect and check snapshot inventory.
        using var socket2 = await factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/game"), ct.Token);
        await SendAsync(socket2, ProtocolMessageNames.GameAuthenticate,
            new AuthenticateCommandPayload(gameToken), 1, ct.Token);
        _ = await ReceiveAsync(socket2, ct.Token);
        await SendAsync(socket2, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, ct.Token);
        var snap2 = await ReceiveAsync(socket2, ct.Token);

        Assert.Equal(ProtocolMessageNames.WorldSnapshot, snap2.Name);
        var payload = Serializer.DeserializePayload<WorldSnapshotPayload>(snap2.Payload)!;
        Assert.NotNull(payload.Inventory);
        Assert.True(payload.Inventory!.Count > 0, "Snapshot inventory should contain the looted item after reconnect");
        Assert.Equal("item.slime_gel", payload.Inventory[0].ItemDefinitionId);
    }
}
