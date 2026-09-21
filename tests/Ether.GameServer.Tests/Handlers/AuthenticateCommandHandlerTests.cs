using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.Domain.Accounts;
using Ether.Domain.Characters;
using Ether.GameServer.Handlers;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;
using Ether.GameServer.Tests.Fakes;

namespace Ether.GameServer.Tests.Handlers;

public sealed class AuthenticateCommandHandlerTests
{
    private static readonly Guid Account = Guid.NewGuid();
    private static readonly Guid Character = Guid.NewGuid();

    private static ProtocolEnvelope Command(object? payload, long sequence = 1) =>
        new ProtocolSerializer().Build(ProtocolMessageTypes.Command, ProtocolMessageNames.GameAuthenticate, payload, Guid.NewGuid(), sequence);

    private static (AuthenticateCommandHandler Handler, StubTokenService Tokens, GameSession Session, CapturingResponder Responder) Create()
    {
        var tokens = new StubTokenService
        {
            GameStatus = TokenValidationStatus.Valid,
            GameClaims = new GameTokenClaims(new AccountId(Account), Character),
        };

        var handler = new AuthenticateCommandHandler(tokens, new ProtocolSerializer(), TimeProvider.System);
        return (handler, tokens, new GameSession(16, DateTimeOffset.UtcNow), new CapturingResponder());
    }

    [Fact]
    public async Task Valid_game_token_authenticates_the_session()
    {
        var (handler, _, session, responder) = Create();

        await handler.HandleAsync(session, Command(new AuthenticateCommandPayload("game-token")), responder, CancellationToken.None);

        Assert.Equal(GameSessionState.Authenticated, session.State);
        Assert.Equal(Account, session.AccountId!.Value.Value);
        var sent = Assert.Single(responder.Events);
        Assert.Equal(ProtocolMessageNames.GameAuthenticated, sent.Name);
    }

    [Fact]
    public async Task Missing_token_returns_invalid_payload()
    {
        var (handler, _, session, responder) = Create();

        await handler.HandleAsync(session, Command(new AuthenticateCommandPayload("")), responder, CancellationToken.None);

        Assert.Equal(GameSessionState.Connecting, session.State);
        Assert.Equal(ProtocolErrorCodes.InvalidPayload, Assert.Single(responder.Errors).Code);
    }

    [Theory]
    [InlineData(TokenValidationStatus.Invalid, ProtocolErrorCodes.InvalidToken)]
    [InlineData(TokenValidationStatus.Expired, ProtocolErrorCodes.TokenExpired)]
    [InlineData(TokenValidationStatus.WrongPurpose, ProtocolErrorCodes.WrongTokenPurpose)]
    public async Task Rejected_tokens_map_to_deterministic_codes(TokenValidationStatus status, string expectedCode)
    {
        var (handler, tokens, session, responder) = Create();
        tokens.GameStatus = status;

        await handler.HandleAsync(session, Command(new AuthenticateCommandPayload("token")), responder, CancellationToken.None);

        Assert.Equal(GameSessionState.Connecting, session.State);
        Assert.Equal(expectedCode, Assert.Single(responder.Errors).Code);
    }

    [Fact]
    public async Task Cannot_authenticate_twice()
    {
        var (handler, _, session, responder) = Create();
        await handler.HandleAsync(session, Command(new AuthenticateCommandPayload("game-token")), responder, CancellationToken.None);

        await handler.HandleAsync(session, Command(new AuthenticateCommandPayload("game-token"), sequence: 2), responder, CancellationToken.None);

        Assert.Equal(ProtocolErrorCodes.AlreadyAuthenticated, responder.Errors[^1].Code);
    }
}
