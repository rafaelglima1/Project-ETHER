using System.Text.Json;

namespace Ether.Contracts.Realtime;

/// <summary>
/// Canonical realtime protocol envelope (ADR-0003). Every frame exchanged over the
/// GameServer WebSocket uses this shape, regardless of message name.
/// </summary>
public sealed record ProtocolEnvelope
{
    /// <summary>Protocol version. Bumped on breaking changes.</summary>
    public int Version { get; init; } = ProtocolMessageTypes.CurrentVersion;

    /// <summary>One of <see cref="ProtocolMessageTypes"/>: command, event or error.</summary>
    public string Type { get; init; } = ProtocolMessageTypes.Command;

    /// <summary>Semantic message name, e.g. <c>movement.move</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Correlates a command with its accepted/rejected outcome.</summary>
    public Guid? RequestId { get; init; }

    /// <summary>Monotonic per-session sequence number.</summary>
    public long Sequence { get; init; }

    /// <summary>Message-specific payload (absent for messages without one).</summary>
    public JsonElement? Payload { get; init; }
}

/// <summary>Envelope <c>type</c> values.</summary>
public static class ProtocolMessageTypes
{
    public const int CurrentVersion = 1;

    public const string Command = "command";
    public const string Event = "event";
    public const string Error = "error";
}

/// <summary>Semantic message names.</summary>
public static class ProtocolMessageNames
{
    // client -> server
    public const string GameAuthenticate = "game.authenticate";
    public const string WorldEnter = "world.enter";
    public const string MovementMove = "movement.move";
    public const string SystemPing = "system.ping";

    // server -> client
    public const string GameAuthenticated = "game.authenticated";
    public const string WorldSnapshot = "world.snapshot";
    public const string MovementAccepted = "movement.accepted";
    public const string SystemPong = "system.pong";
    public const string ProtocolError = "protocol.error";

    // server -> client rejections (error type, matching the failed command)
    public const string GameAuthenticateRejected = "game.authenticate.rejected";
    public const string WorldEnterRejected = "world.enter.rejected";
    public const string MovementRejected = "movement.rejected";
}

/// <summary>Deterministic error codes returned to clients (never stack traces).</summary>
public static class ProtocolErrorCodes
{
    public const string InvalidEnvelope = "INVALID_ENVELOPE";
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
    public const string UnknownMessage = "UNKNOWN_MESSAGE";
    public const string InvalidPayload = "INVALID_PAYLOAD";
    public const string MessageTooLarge = "MESSAGE_TOO_LARGE";
    public const string NotAuthenticated = "NOT_AUTHENTICATED";
    public const string AlreadyAuthenticated = "ALREADY_AUTHENTICATED";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string WrongTokenPurpose = "WRONG_TOKEN_PURPOSE";
    public const string NotAuthorized = "NOT_AUTHORIZED";
    public const string NotInWorld = "NOT_IN_WORLD";
    public const string AlreadyInWorld = "ALREADY_IN_WORLD";
    public const string InvalidMap = "INVALID_MAP";
    public const string OutOfBounds = "OUT_OF_BOUNDS";
    public const string TooFar = "TOO_FAR";
    public const string InvalidState = "INVALID_STATE";
    public const string InvalidSequence = "INVALID_SEQUENCE";
    public const string RateLimited = "RATE_LIMITED";
    public const string ServerBusy = "SERVER_BUSY";
    public const string InternalError = "INTERNAL_ERROR";
}
