using System.Collections.Concurrent;

using Ether.Contracts.Configuration;

using Microsoft.Extensions.Options;

namespace Ether.GameServer.Sessions;

/// <summary>Registry of live realtime sessions, bounded by configuration.</summary>
public sealed class GameSessionManager
{
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly GameServerOptions _options;

    public GameSessionManager(IOptions<GameServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public int Count => _sessions.Count;

    public GameSession? Create(TimeProvider timeProvider)
    {
        if (_sessions.Count >= _options.MaxConnections)
        {
            return null;
        }

        var session = new GameSession(_options.MaxQueuedCommands, timeProvider.GetUtcNow());
        return _sessions.TryAdd(session.SessionId, session) ? session : null;
    }

    public void Remove(Guid sessionId) => _sessions.TryRemove(sessionId, out _);

    public IReadOnlyList<GameSession> Snapshot() => _sessions.Values.ToList();

    /// <summary>Finds the live session controlling a character, if any.</summary>
    public GameSession? FindByCharacter(Ether.Domain.Characters.CharacterId characterId) =>
        _sessions.Values.FirstOrDefault(session => session.CharacterId == characterId);
}
