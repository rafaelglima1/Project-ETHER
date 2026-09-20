namespace Ether.Contracts.Configuration;

/// <summary>
/// Binds the <c>WebSocket</c> configuration section.
/// The gameplay protocol runs over raw ASP.NET Core WebSockets (SignalR is not used).
/// </summary>
public sealed class WebSocketOptions
{
    public const string SectionName = "WebSocket";

    /// <summary>Relative path used by the GameServer endpoint (e.g. <c>/game</c>).</summary>
    public string Path { get; set; } = "/game";

    /// <summary>Client ping interval, in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 20;

    /// <summary>Server timeout before declaring a session stale, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Receive buffer size, in bytes.</summary>
    public int ReceiveBufferSize { get; set; } = 4096;
}
