using System.Net.WebSockets;
using System.Text;

using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;
using Ether.GameServer.Tests.Fakes;
using Ether.Domain.Characters;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ether.GameServer.Tests;

/// <summary>
/// Death/respawn reconnect integration: die → world.enter rejected → respawn →
/// snapshot → move → disconnect → reconnect → world.enter.
/// </summary>
public sealed class DeathRespawnIntegrationTests
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
            var result = await socket.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        var envelope = Serializer.TryDeserialize(json, out _);

        // Skip unsolicited events (creature broadcasts).
        while (envelope is not null && envelope.RequestId is null)
        {
            var r = await socket.ReceiveAsync(buffer, ct);
            var next = Encoding.UTF8.GetString(buffer, 0, r.Count);
            envelope = Serializer.TryDeserialize(next, out _);
            if (!r.EndOfMessage) continue;
        }

        return envelope!;
    }

    private static async Task<WebSocket> ConnectAsync(WebApplicationFactory<Program> factory, string token, CancellationToken ct)
    {
        var socket = await factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/game"), ct);
        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(token), 1, ct);
        _ = await ReceiveAsync(socket, ct);
        return socket;
    }

    [Fact]
    public async Task Dead_character_world_enter_returns_character_dead_not_internal_error()
    {
        using var factory = CreateFactory();
        using var ct = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Dead");
        // Kill the character directly.
        var char_ = await persistence.GetByIdAsync(character, ct.Token);
        char_!.EnterWorld(DateTimeOffset.UtcNow);
        char_.ApplyDamage(char_.MaxHealth, DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        using var socket = await ConnectAsync(factory, tokenService.CreateGameToken(account, character.Value).Token, ct.Token);

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, ct.Token);
        var response = await ReceiveAsync(socket, ct.Token);

        Assert.Equal(ProtocolMessageTypes.Error, response.Type);
        var error = Serializer.DeserializePayload<ProtocolErrorPayload>(response.Payload)!;
        Assert.Equal(ProtocolErrorCodes.CharacterDead, error.Code);
    }

    [Fact]
    public async Task Dead_character_respawn_restores_and_allows_movement()
    {
        using var factory = CreateFactory();
        using var ct = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var char_ = await persistence.GetByIdAsync(character, ct.Token);
        char_!.EnterWorld(DateTimeOffset.UtcNow);
        char_.ApplyDamage(char_.MaxHealth, DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        using var socket = await ConnectAsync(factory, tokenService.CreateGameToken(account, character.Value).Token, ct.Token);

        // Respawn.
        await SendAsync(socket, ProtocolMessageNames.CharacterRespawn, new { }, 2, ct.Token);
        var snapshot = await ReceiveAsync(socket, ct.Token);
        Assert.Equal(ProtocolMessageNames.WorldSnapshot, snapshot.Name);

        var player = Serializer.DeserializePayload<WorldSnapshotPayload>(snapshot.Payload)!.Player;
        Assert.Equal(char_.MaxHealth, player.Health);
        Assert.Equal("InWorld", player.State);

        // Move after respawn.
        await SendAsync(socket, ProtocolMessageNames.MovementMove, new MovementMoveCommandPayload(3, 4), 3, ct.Token);
        var moved = await ReceiveAsync(socket, ct.Token);
        Assert.Equal(ProtocolMessageNames.MovementAccepted, moved.Name);

        // Combat after respawn (attack a creature from the snapshot).
        var creatures = Serializer.DeserializePayload<WorldSnapshotPayload>(snapshot.Payload)!.Creatures;
        Assert.NotEmpty(creatures!);
        // Creature may be far; verify combat.attack returns a deterministic result (not INTERNAL_ERROR).
        await SendAsync(socket, ProtocolMessageNames.CombatAttack,
            new AttackCommandPayload("warrior.basic_attack", creatures![0].CreatureId, "creature"), 4, ct.Token);
        var combat = await ReceiveAsync(socket, ct.Token);
        // Either combat.result or combat.rejected with a known code — not INTERNAL_ERROR.
        Assert.True(
            combat.Name == ProtocolMessageNames.CombatResult ||
            Serializer.DeserializePayload<ProtocolErrorPayload>(combat.Payload)!.Code != ProtocolErrorCodes.InternalError,
            $"Expected combat result or non-internal error, got {combat.Name}");
    }

    [Fact]
    public async Task Reconnect_after_respawn_succeeds()
    {
        using var factory = CreateFactory();
        using var ct = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var char_ = await persistence.GetByIdAsync(character, ct.Token);
        char_!.EnterWorld(DateTimeOffset.UtcNow);
        char_.ApplyDamage(char_.MaxHealth, DateTimeOffset.UtcNow);

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var gameToken = tokenService.CreateGameToken(account, character.Value).Token;

        // First connection: respawn.
        using (var socket1 = await ConnectAsync(factory, gameToken, ct.Token))
        {
            await SendAsync(socket1, ProtocolMessageNames.CharacterRespawn, new { }, 2, ct.Token);
            var snap = await ReceiveAsync(socket1, ct.Token);
            Assert.Equal(ProtocolMessageNames.WorldSnapshot, snap.Name);
        }

        // Disconnect and reconnect.
        using var socket2 = await ConnectAsync(factory, gameToken, ct.Token);
        await SendAsync(socket2, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, ct.Token);
        var reentered = await ReceiveAsync(socket2, ct.Token);

        Assert.Equal(ProtocolMessageNames.WorldSnapshot, reentered.Name);
        var player = Serializer.DeserializePayload<WorldSnapshotPayload>(reentered.Payload)!.Player;
        Assert.Equal("InWorld", player.State);
        Assert.Equal(100, player.Health);
    }

    [Fact]
    public async Task Duplicate_world_enter_returns_already_in_world()
    {
        using var factory = CreateFactory();
        using var ct = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (account, character) = await persistence.SeedCharacterAsync("Hero");
        var tokenService = factory.Services.GetRequiredService<ITokenService>();

        using var socket = await ConnectAsync(factory, tokenService.CreateGameToken(account, character.Value).Token, ct.Token);

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, ct.Token);
        _ = await ReceiveAsync(socket, ct.Token);

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 3, ct.Token);
        var dup = await ReceiveAsync(socket, ct.Token);

        Assert.Equal(ProtocolMessageTypes.Error, dup.Type);
        var error = Serializer.DeserializePayload<ProtocolErrorPayload>(dup.Payload)!;
        Assert.Equal(ProtocolErrorCodes.AlreadyInWorld, error.Code);
    }
}
