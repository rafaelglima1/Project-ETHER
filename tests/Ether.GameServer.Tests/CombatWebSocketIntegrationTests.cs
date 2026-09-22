using System.Net.WebSockets;
using System.Text;

using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.Domain.Combat;
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
/// Real WebSocket combat flow: authenticate → world.enter → combat.attack →
/// combat.result, plus concurrency and identity guarantees.
/// </summary>
public sealed class CombatWebSocketIntegrationTests
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
                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IItemInstanceRepository>();

                services.AddSingleton(persistence);
                services.AddSingleton<IAccountRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<ICharacterRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IItemInstanceRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
            });
        });
    }

    private static async Task SendAsync(WebSocket socket, string name, object? payload, long sequence, CancellationToken cancellationToken)
    {
        var envelope = Serializer.Build(ProtocolMessageTypes.Command, name, payload, Guid.NewGuid(), sequence);
        await socket.SendAsync(Encoding.UTF8.GetBytes(Serializer.Serialize(envelope)), WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// Reads the next frame whose requestId is set (a response to a command),
    /// skipping unsolicited server events such as creature broadcasts.
    /// </summary>
    private static async Task<ProtocolEnvelope> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        while (true)
        {
            var envelope = await ReceiveRawAsync(socket, cancellationToken);
            if (envelope.RequestId is not null)
            {
                return envelope;
            }
        }
    }

    private static async Task<ProtocolEnvelope> ReceiveRawAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        var envelope = Serializer.TryDeserialize(json, out var reason);
        Assert.True(envelope is not null, $"Server sent an unparseable frame: {reason}");
        return envelope!;
    }

    private static async Task<WebSocket> ConnectAndEnterWorldAsync(
        WebApplicationFactory<Program> factory,
        string gameToken,
        CancellationToken cancellationToken) =>
        (await ConnectEnterWithSnapshotAsync(factory, gameToken, cancellationToken)).Socket;

    private static async Task<(WebSocket Socket, WorldSnapshotPayload Snapshot)> ConnectEnterWithSnapshotAsync(
        WebApplicationFactory<Program> factory,
        string gameToken,
        CancellationToken cancellationToken)
    {
        var socket = await factory.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri("ws://localhost/game"), cancellationToken);

        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(gameToken), 1, cancellationToken);
        var authenticated = await ReceiveAsync(socket, cancellationToken);
        Assert.Equal(ProtocolMessageNames.GameAuthenticated, authenticated.Name);

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, cancellationToken);
        var snapshot = await ReceiveAsync(socket, cancellationToken);
        Assert.Equal(ProtocolMessageNames.WorldSnapshot, snapshot.Name);

        return (socket, Serializer.DeserializePayload<WorldSnapshotPayload>(snapshot.Payload)!);
    }

    [Fact]
    public async Task Combat_attack_returns_a_server_resolved_result()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (attackerAccount, attackerId) = await persistence.SeedCharacterAsync("Attacker");
        var (_, targetId) = await persistence.SeedCharacterAsync("Target");

        // Target is placed in the world directly (no session needed to be attackable).
        var target = await persistence.GetByIdAsync(targetId, timeout.Token);
        target!.EnterWorld(DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var gameToken = tokenService.CreateGameToken(attackerAccount, attackerId.Value).Token;

        using var socket = await ConnectAndEnterWorldAsync(factory, gameToken, timeout.Token);

        await SendAsync(
            socket,
            ProtocolMessageNames.CombatAttack,
            new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, targetId.Value),
            3,
            timeout.Token);

        var result = await ReceiveAsync(socket, timeout.Token);

        Assert.Equal(ProtocolMessageNames.CombatResult, result.Name);
        var payload = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>(result.Payload)!;

        Assert.Equal(attackerId.Value, payload.AttackerId);
        Assert.Equal(targetId.Value, payload.TargetId);
        Assert.True(payload.Damage >= 1);
        Assert.Equal(target!.MaxHealth - payload.Damage, payload.TargetHealth);
        Assert.Equal("Combat", target.State.ToString());
    }

    [Fact]
    public async Task Invalid_ability_and_cooldown_are_rejected_with_codes()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (attackerAccount, attackerId) = await persistence.SeedCharacterAsync("Attacker");
        var (_, targetId) = await persistence.SeedCharacterAsync("Target");
        (await persistence.GetByIdAsync(targetId, timeout.Token))!.EnterWorld(DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var gameToken = tokenService.CreateGameToken(attackerAccount, attackerId.Value).Token;
        using var socket = await ConnectAndEnterWorldAsync(factory, gameToken, timeout.Token);

        await SendAsync(socket, ProtocolMessageNames.CombatAttack, new AttackCommandPayload("warrior.nope", targetId.Value), 3, timeout.Token);
        var unknown = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolErrorCodes.AbilityNotFound, Serializer.DeserializePayload<ProtocolErrorPayload>(unknown.Payload)!.Code);

        await SendAsync(socket, ProtocolMessageNames.CombatAttack, new AttackCommandPayload(AbilityCatalog.PowerStrike.Value, targetId.Value), 4, timeout.Token);
        var first = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolMessageNames.CombatResult, first.Name);

        await SendAsync(socket, ProtocolMessageNames.CombatAttack, new AttackCommandPayload(AbilityCatalog.PowerStrike.Value, targetId.Value), 5, timeout.Token);
        var cooldown = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolErrorCodes.CooldownActive, Serializer.DeserializePayload<ProtocolErrorPayload>(cooldown.Payload)!.Code);
    }

    [Fact]
    public async Task Concurrent_attacks_do_not_lose_damage()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (accountA, characterA) = await persistence.SeedCharacterAsync("A");
        var (accountB, characterB) = await persistence.SeedCharacterAsync("B");
        var (_, targetId) = await persistence.SeedCharacterAsync("Target");
        (await persistence.GetByIdAsync(targetId, timeout.Token))!.EnterWorld(DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        using var socketA = await ConnectAndEnterWorldAsync(factory, tokenService.CreateGameToken(accountA, characterA.Value).Token, timeout.Token);
        using var socketB = await ConnectAndEnterWorldAsync(factory, tokenService.CreateGameToken(accountB, characterB.Value).Token, timeout.Token);

        var attackA = SendAsync(socketA, ProtocolMessageNames.CombatAttack, new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, targetId.Value), 3, timeout.Token);
        var attackB = SendAsync(socketB, ProtocolMessageNames.CombatAttack, new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, targetId.Value), 3, timeout.Token);
        await Task.WhenAll(attackA, attackB);

        var resultA = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>((await ReceiveAsync(socketA, timeout.Token)).Payload)!;
        var resultB = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>((await ReceiveAsync(socketB, timeout.Token)).Payload)!;

        var target = await persistence.GetByIdAsync(targetId, timeout.Token);
        Assert.Equal(target!.MaxHealth - (resultA.Damage + resultB.Damage), target.Health);
    }

    [Fact]
    public async Task Attacker_identity_comes_from_the_session_not_the_payload()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (accountA, characterA) = await persistence.SeedCharacterAsync("A");
        var (_, characterB) = await persistence.SeedCharacterAsync("B");
        (await persistence.GetByIdAsync(characterB, timeout.Token))!.EnterWorld(DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        using var socket = await ConnectAndEnterWorldAsync(factory, tokenService.CreateGameToken(accountA, characterA.Value).Token, timeout.Token);

        await SendAsync(socket, ProtocolMessageNames.CombatAttack, new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, characterB.Value), 3, timeout.Token);
        var result = await ReceiveAsync(socket, timeout.Token);

        var payload = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>(result.Payload)!;
        Assert.Equal(characterA.Value, payload.AttackerId);
    }

    [Fact]
    public async Task World_snapshot_includes_the_map_creatures()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();

        var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/game"), timeout.Token);
        using var socketScope = socket;

        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(tokenService.CreateGameToken(account, character.Value).Token), 1, timeout.Token);
        _ = await ReceiveAsync(socket, timeout.Token);

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, timeout.Token);
        var snapshot = await ReceiveAsync(socket, timeout.Token);

        var payload = Serializer.DeserializePayload<WorldSnapshotPayload>(snapshot.Payload)!;
        Assert.NotNull(payload.Creatures);
        Assert.NotEmpty(payload.Creatures!);
    }

    [Fact]
    public async Task Attacking_a_creature_is_routed_to_the_creature_pipeline()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();

        var (socket, snapshot) = await ConnectEnterWithSnapshotAsync(
            factory, tokenService.CreateGameToken(account, character.Value).Token, timeout.Token);
        using var socketScope = socket;

        // Pick the creature farthest from the spawn point so the AI cannot bring it
        // into range before we attack.
        var farthest = snapshot.Creatures!
            .OrderByDescending(creature => Math.Max(creature.X, creature.Y))
            .First();

        await SendAsync(
            socket,
            ProtocolMessageNames.CombatAttack,
            new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, farthest.CreatureId, "creature"),
            3,
            timeout.Token);

        var rejected = await ReceiveAsync(socket, timeout.Token);

        Assert.Equal(ProtocolMessageTypes.Error, rejected.Type);
        // OUT_OF_RANGE proves the id was resolved as a creature (a character id
        // would have produced TARGET_NOT_FOUND).
        Assert.Equal(
            ProtocolErrorCodes.OutOfRange,
            Serializer.DeserializePayload<ProtocolErrorPayload>(rejected.Payload)!.Code);
    }

    [Fact]
    public async Task Killing_a_creature_grants_experience_and_loot_once()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();

        var (socket, _) = await ConnectEnterWithSnapshotAsync(
            factory, tokenService.CreateGameToken(account, character.Value).Token, timeout.Token);
        using var socketScope = socket;

        // Seed a creature right next to the player (0,0) so the kill is deterministic.
        var creatureWorld = factory.Services.GetRequiredService<ICreatureWorld>();
        var slimeDefinition = CreatureCatalog.Get(new CreatureDefinitionId("creature.slime"));
        var creature = new CreatureInstance(
            CreatureInstanceId.New(),
            slimeDefinition.Id,
            new MapId(1),
            new WorldPosition(new MapId(1), 1, 0),
            slimeDefinition.MaxHealth);
        creatureWorld.Add(creature);

        Contracts.Combat.CombatResultResponse? final = null;
        for (var sequence = 3; sequence <= 12 && final is null; sequence++)
        {
            await SendAsync(
                socket,
                ProtocolMessageNames.CombatAttack,
                new AttackCommandPayload(AbilityCatalog.BasicAttack.Value, creature.Id.Value, "creature"),
                sequence,
                timeout.Token);

            var response = await ReceiveAsync(socket, timeout.Token);
            if (response.Type == ProtocolMessageTypes.Error)
            {
                continue;
            }

            var payload = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>(response.Payload)!;
            if (payload.TargetDefeated)
            {
                final = payload;
            }
        }

        Assert.NotNull(final);
        Assert.Equal(slimeDefinition.ExperienceReward, final!.ExperienceGained);

        var stored = await persistence.GetByIdAsync(character, timeout.Token);
        Assert.Equal(slimeDefinition.ExperienceReward, stored!.Experience);

        // Loot is random, but whatever was reported must be persisted for the owner.
        var items = await persistence.GetItemsByOwnerAsync(character, timeout.Token);
        var reported = final.Loot?.Sum(loot => loot.Quantity) ?? 0;
        Assert.Equal(reported, items.Sum(item => item.Quantity));
    }

    [Fact]
    public async Task World_snapshot_includes_progression()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();

        var (socket, snapshot) = await ConnectEnterWithSnapshotAsync(
            factory, tokenService.CreateGameToken(account, character.Value).Token, timeout.Token);
        using var socketScope = socket;

        Assert.Equal(1, snapshot.Player.Level);
        Assert.Equal(0, snapshot.Player.Experience);
        Assert.True(snapshot.Player.ExperienceToNextLevel > 0);
        Assert.True(snapshot.Player.MaxHealth > 0);
        Assert.Equal(snapshot.Player.MaxHealth, snapshot.Player.Health);
    }

    [Fact]
    public async Task Creature_ai_eventually_attacks_a_nearby_player()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        using var socket = await ConnectAndEnterWorldAsync(factory, tokenService.CreateGameToken(account, character.Value).Token, timeout.Token);

        // The nearest slime is within aggro range of the spawn point and will chase
        // and attack; wait for the unsolicited creature combat result.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var envelope = await ReceiveRawAsync(socket, timeout.Token);
            if (envelope.Name != ProtocolMessageNames.CombatResult || envelope.Type != ProtocolMessageTypes.Event)
            {
                continue;
            }

            var payload = Serializer.DeserializePayload<Contracts.Combat.CombatResultResponse>(envelope.Payload)!;
            if (payload.AttackerType == "creature")
            {
                Assert.Equal(character.Value, payload.TargetId);
                Assert.True(payload.Damage >= 1);
                Assert.True(payload.TargetHealth < payload.TargetMaxHealth);
                return;
            }
        }

        Assert.Fail("Creature AI did not attack the nearby player within the timeout.");
    }
}
