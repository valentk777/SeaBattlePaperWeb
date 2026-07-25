using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Application.Matches;

public interface ISeaBattleStore
{
    Task<Match?> GetMatchAsync(string matchId, CancellationToken cancellationToken);

    Task<Match?> GetMatchForJoinAsync(string matchId, CancellationToken cancellationToken);

    Task<Match?> GetMatchSnapshotAsync(string matchId, CancellationToken cancellationToken);

    Task<bool> MatchExistsAsync(string matchId, CancellationToken cancellationToken);

    Task AddMatchAsync(Match match, CancellationToken cancellationToken);

    Task AddPlayerAsync(Player player, CancellationToken cancellationToken);

    Task UpdatePlayerLastSeenAsync(Guid playerId, DateTimeOffset lastSeenAtUtc, CancellationToken cancellationToken);

    Task ReplacePlayerFleetAndReadyAsync(
        Guid playerId,
        IReadOnlyCollection<Ship> ships,
        DateTimeOffset readyAtUtc,
        CancellationToken cancellationToken);

    Task ReplacePlayerFleetAsync(
        Guid playerId,
        IReadOnlyCollection<Ship> ships,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken);

    Task StartMatchAsync(
        string matchId,
        Guid currentTurnPlayerId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken);

    Task AddShotAsync(Shot shot, CancellationToken cancellationToken);

    Task UpdateMatchAfterShotAsync(
        string matchId,
        Guid? currentTurnPlayerId,
        Guid? winnerPlayerId,
        MatchStatus status,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? finishedAtUtc,
        CancellationToken cancellationToken);

    void RemovePlayer(Player player);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
