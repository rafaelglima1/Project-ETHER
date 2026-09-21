using System.Net.WebSockets;
using System.Text;

using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.GameServer.Protocol;
using Ether.GameServer.Tests.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ether.GameServer.Tests;

/// <summary>
/// Real WebSocket integration test: connect → authenticate → enter world →
/// snapshot → move → accepted. Uses in-memory persistence but the real transport,
/// protocol and JWT validation.
/// </summary>
public sealed class GameWebSocketIntegrationTests
{
    private static readonly ProtocolSerializer Serializer = new();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

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

                services.AddSingleton(persistence);
                services.AddSingleton<IAccountRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<ICharacterRepository>(provider => provider.GetRequiredService<InMemoryPersistence>());
                services.AddSingleton<IUnitOfWork>(provider => provider.GetRequiredService<InMemoryPersistence>());
            });
        });
    }

    private static async Task SendAsync(WebSocket socket, string name, object? payload, long sequence, CancellationToken cancellationToken)
    {
        var envelope = Serializer.Build(ProtocolMessageTypes.Command, name, payload, Guid.NewGuid(), sequence);
        var json = Serializer.Serialize(envelope);
        await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<ProtocolEnvelope> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
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

    [Fact]
    public async Task Full_flow_authenticate_enter_world_and_move()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (accountId, characterId) = await persistence.SeedCharacterAsync();

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var gameToken = tokenService.CreateGameToken(accountId, characterId.Value).Token;

        var webSocketClient = factory.Server.CreateWebSocketClient();
        using var socket = await webSocketClient.ConnectAsync(new Uri("ws://localhost/game"), timeout.Token);

        // Not authenticated yet: world.enter must be refused.
        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 1, timeout.Token);
        var refused = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolMessageTypes.Error, refused.Type);
        Assert.Equal(
            ProtocolErrorCodes.NotAuthenticated,
            Serializer.DeserializePayload<ProtocolErrorPayload>(refused.Payload)!.Code);

        // Authenticate.
        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(gameToken), 2, timeout.Token);
        var authenticated = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolMessageNames.GameAuthenticated, authenticated.Name);
        var authPayload = Serializer.DeserializePayload<AuthenticatedEventPayload>(authenticated.Payload)!;
        Assert.Equal(characterId.Value, authPayload.CharacterId);

        // Duplicate sequence must be refused.
        await SendAsync(socket, ProtocolMessageNames.SystemPing, new { }, 2, timeout.Token);
        var stale = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolErrorCodes.InvalidSequence, Serializer.DeserializePayload<ProtocolErrorPayload>(stale.Payload)!.Code);

        // Enter world → snapshot.
        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 3, timeout.Token);
        var snapshot = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolMessageNames.WorldSnapshot, snapshot.Name);
        Assert.Equal(1, Serializer.DeserializePayload<WorldSnapshotPayload>(snapshot.Payload)!.MapId);

        // Move.
        await SendAsync(socket, ProtocolMessageNames.MovementMove, new MovementMoveCommandPayload(4, 6), 4, timeout.Token);
        var accepted = await ReceiveAsync(socket, timeout.Token);
        Assert.Equal(ProtocolMessageNames.MovementAccepted, accepted.Name);
        var move = Serializer.DeserializePayload<MovementAcceptedEventPayload>(accepted.Payload)!;
        Assert.Equal(4, move.X);
        Assert.Equal(6, move.Y);

        // Persisted position is authoritative server-side.
        var stored = await persistence.GetByIdAsync(characterId, timeout.Token);
        Assert.Equal(4, stored!.Position.X);
        Assert.Equal(6, stored.Position.Y);

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token);
    }

    [Fact]
    public async Task Access_token_cannot_open_a_game_session()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (accountId, _) = await persistence.SeedCharacterAsync();

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        var accessToken = tokenService.CreateAccessToken(accountId).Token;

        var webSocketClient = factory.Server.CreateWebSocketClient();
        using var socket = await webSocketClient.ConnectAsync(new Uri("ws://localhost/game"), timeout.Token);

        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(accessToken), 1, timeout.Token);
        var rejected = await ReceiveAsync(socket, timeout.Token);

        Assert.Equal(ProtocolMessageTypes.Error, rejected.Type);
        Assert.Equal(
            ProtocolErrorCodes.WrongTokenPurpose,
            Serializer.DeserializePayload<ProtocolErrorPayload>(rejected.Payload)!.Code);
    }

    [Fact]
    public async Task Account_a_cannot_control_character_b()
    {
        using var factory = CreateFactory();
        using var timeout = new CancellationTokenSource(Timeout);

        var persistence = factory.Services.GetRequiredService<InMemoryPersistence>();
        var (_, characterA) = await persistence.SeedCharacterAsync("HeroA");
        var (accountB, _) = await persistence.SeedCharacterAsync("HeroB");

        var tokenService = factory.Services.GetRequiredService<ITokenService>();
        // Token minted for account B but pointing at character A (forged intent).
        var forgedToken = tokenService.CreateGameToken(accountB, characterA.Value).Token;

        var webSocketClient = factory.Server.CreateWebSocketClient();
        using var socket = await webSocketClient.ConnectAsync(new Uri("ws://localhost/game"), timeout.Token);

        await SendAsync(socket, ProtocolMessageNames.GameAuthenticate, new AuthenticateCommandPayload(forgedToken), 1, timeout.Token);
        _ = await ReceiveAsync(socket, timeout.Token); // authenticated (token is valid)

        await SendAsync(socket, ProtocolMessageNames.WorldEnter, new WorldEnterCommandPayload(), 2, timeout.Token);
        var rejected = await ReceiveAsync(socket, timeout.Token);

        Assert.Equal(ProtocolMessageTypes.Error, rejected.Type);
        Assert.Equal(
            ProtocolErrorCodes.NotAuthorized,
            Serializer.DeserializePayload<ProtocolErrorPayload>(rejected.Payload)!.Code);
    }
}
