using System.Collections.Concurrent;

namespace SeaBattlePaper.Api.Hubs;

public sealed class SeaBattleConnectionRegistry
{
    private readonly ConcurrentDictionary<string, PlayerConnection> connections = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> connectionsByPlayer = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> playersByMatch = new();

    public void Attach(string connectionId, string matchId, Guid playerId)
    {
        Detach(connectionId);

        var connection = new PlayerConnection(connectionId, matchId, playerId);
        connections[connectionId] = connection;
        connectionsByPlayer.GetOrAdd(playerId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;
        playersByMatch.GetOrAdd(matchId, _ => new ConcurrentDictionary<Guid, byte>())[playerId] = 0;
    }

    public PlayerConnection? GetConnection(string connectionId) => connections.TryGetValue(connectionId, out var connection) ? connection : null;

    public PlayerConnection? Detach(string connectionId)
    {
        if (!connections.TryRemove(connectionId, out var connection)) return null;

        if (connectionsByPlayer.TryGetValue(connection.PlayerId, out var playerConnections))
        {
            playerConnections.TryRemove(connectionId, out _);
            if (playerConnections.IsEmpty)
            {
                connectionsByPlayer.TryRemove(connection.PlayerId, out _);
                if (playersByMatch.TryGetValue(connection.MatchId, out var matchPlayers))
                {
                    matchPlayers.TryRemove(connection.PlayerId, out _);
                    if (matchPlayers.IsEmpty) playersByMatch.TryRemove(connection.MatchId, out _);
                }
            }
        }

        return connection;
    }

    public IReadOnlyCollection<string> GetPlayerConnections(Guid playerId) =>
        connectionsByPlayer.TryGetValue(playerId, out var playerConnections)
            ? playerConnections.Keys.ToArray()
            : Array.Empty<string>();

    public IReadOnlyCollection<PlayerConnection> GetMatchConnections(string matchId) =>
        connections.Values.Where(connection => connection.MatchId == matchId).ToArray();

    public IReadOnlySet<Guid> GetOnlinePlayers(string matchId) =>
        playersByMatch.TryGetValue(matchId, out var matchPlayers)
            ? matchPlayers.Keys.ToHashSet()
            : new HashSet<Guid>();
}

public sealed record PlayerConnection(string ConnectionId, string MatchId, Guid PlayerId);
