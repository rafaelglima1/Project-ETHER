namespace Ether.GameServer.Sessions;

/// <summary>Explicit realtime session lifecycle (ADR-0003).</summary>
public enum GameSessionState
{
    Connecting = 0,
    Authenticated = 1,
    InWorld = 2,
    Disconnecting = 3,
    Disconnected = 4,
}
