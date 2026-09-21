using Ether.Application.Abstractions;
using Ether.Contracts.Realtime;
using Ether.Domain.Characters;
using Ether.GameServer.Protocol;
using Ether.GameServer.Sessions;

namespace Ether.GameServer.Handlers;

/// <summary>Authenticates a realtime session using a game token.</summary>
public sealed class AuthenticateCommandHandler : IProtocolCommandHandler
{
    private readonly ITokenService _tokenService;
    private readonly ProtocolSerializer _serializer;
    private readonly TimeProvider _timeProvider;

    public AuthenticateCommandHandler(ITokenService tokenService, ProtocolSerializer serializer, TimeProvider timeProvider)
    {
        _tokenService = tokenService;
        _serializer = serializer;
        _timeProvider = timeProvider;
    }

    public string Name => ProtocolMessageNames.GameAuthenticate;

    public async Task HandleAsync(
        GameSession session,
        ProtocolEnvelope envelope,
        IProtocolResponder responder,
        CancellationToken cancellationToken)
    {
        if (session.State != GameSessionState.Connecting)
        {
            await responder.SendErrorAsync(
                ProtocolMessageNames.GameAuthenticateRejected,
                ProtocolErrorCodes.AlreadyAuthenticated,
                "Session is already authenticated.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var payload = _serializer.DeserializePayload<AuthenticateCommandPayload>(envelope.Payload);
        if (payload is null || string.IsNullOrWhiteSpace(payload.GameToken))
        {
            await responder.SendErrorAsync(
                ProtocolMessageNames.GameAuthenticateRejected,
                ProtocolErrorCodes.InvalidPayload,
                "gameToken is required.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var validation = _tokenService.ValidateGameToken(payload.GameToken);
        if (validation.Status != TokenValidationStatus.Valid || validation.Claims is null)
        {
            var code = validation.Status switch
            {
                TokenValidationStatus.Expired => ProtocolErrorCodes.TokenExpired,
                TokenValidationStatus.WrongPurpose => ProtocolErrorCodes.WrongTokenPurpose,
                _ => ProtocolErrorCodes.InvalidToken,
            };

            await responder.SendErrorAsync(
                ProtocolMessageNames.GameAuthenticateRejected,
                code,
                "Game token rejected.",
                envelope.RequestId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var claims = validation.Claims.Value;
        session.MarkAuthenticated(claims.AccountId, new CharacterId(claims.CharacterId), _timeProvider.GetUtcNow());

        await responder.SendEventAsync(
            ProtocolMessageNames.GameAuthenticated,
            new AuthenticatedEventPayload(session.SessionId, claims.AccountId.Value, claims.CharacterId),
            envelope.RequestId,
            cancellationToken).ConfigureAwait(false);
    }
}
