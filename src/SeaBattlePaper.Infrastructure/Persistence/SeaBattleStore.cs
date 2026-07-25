using Microsoft.EntityFrameworkCore;
using SeaBattlePaper.Application.Matches;
using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Infrastructure.Persistence;

public sealed class SeaBattleStore(SeaBattleDbContext dbContext) : ISeaBattleStore
{
    public async Task<Match?> GetMatchAsync(string matchId, CancellationToken cancellationToken) =>
        await dbContext.Matches
            .AsTracking()
            .Include(match => match.Players)
            .ThenInclude(player => player.Ships)
            .Include(match => match.Shots)
            .SingleOrDefaultAsync(match => match.Id == matchId, cancellationToken);

    public async Task<Match?> GetMatchForJoinAsync(string matchId, CancellationToken cancellationToken) =>
        await dbContext.Matches
            .AsNoTracking()
            .Include(match => match.Players)
            .SingleOrDefaultAsync(match => match.Id == matchId, cancellationToken);

    public async Task<Match?> GetMatchSnapshotAsync(string matchId, CancellationToken cancellationToken) =>
        await dbContext.Matches
            .AsNoTracking()
            .Include(match => match.Players)
            .ThenInclude(player => player.Ships)
            .Include(match => match.Shots)
            .SingleOrDefaultAsync(match => match.Id == matchId, cancellationToken);

    public async Task<bool> MatchExistsAsync(string matchId, CancellationToken cancellationToken) =>
        await dbContext.Matches.AnyAsync(match => match.Id == matchId, cancellationToken);

    public async Task AddMatchAsync(Match match, CancellationToken cancellationToken) => await dbContext.Matches.AddAsync(match, cancellationToken);

    public async Task AddPlayerAsync(Player player, CancellationToken cancellationToken) =>
        await dbContext.Players.AddAsync(player, cancellationToken);

    public async Task UpdatePlayerLastSeenAsync(Guid playerId, DateTimeOffset lastSeenAtUtc, CancellationToken cancellationToken) =>
        await dbContext.Players
            .Where(player => player.Id == playerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(player => player.LastSeenAtUtc, lastSeenAtUtc),
                cancellationToken);

    public async Task ReplacePlayerFleetAndReadyAsync(
        Guid playerId,
        IReadOnlyCollection<Ship> ships,
        DateTimeOffset readyAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.Ships
            .Where(ship => ship.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Ships.AddRangeAsync(ships, cancellationToken);

        await dbContext.Players
            .Where(player => player.Id == playerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(player => player.IsReady, true)
                    .SetProperty(player => player.ReadyAtUtc, readyAtUtc)
                    .SetProperty(player => player.LastSeenAtUtc, readyAtUtc),
                cancellationToken);
    }

    public async Task ReplacePlayerFleetAsync(
        Guid playerId,
        IReadOnlyCollection<Ship> ships,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.Ships
            .Where(ship => ship.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Ships.AddRangeAsync(ships, cancellationToken);

        await dbContext.Players
            .Where(player => player.Id == playerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(player => player.LastSeenAtUtc, updatedAtUtc),
                cancellationToken);
    }

    public async Task StartMatchAsync(
        string matchId,
        Guid currentTurnPlayerId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken) =>
        await dbContext.Matches
            .Where(match => match.Id == matchId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(match => match.Status, MatchStatus.InProgress)
                    .SetProperty(match => match.StartedAtUtc, startedAtUtc)
                    .SetProperty(match => match.UpdatedAtUtc, startedAtUtc)
                    .SetProperty(match => match.CurrentTurnPlayerId, currentTurnPlayerId),
                cancellationToken);

    public async Task AddShotAsync(Shot shot, CancellationToken cancellationToken) =>
        await dbContext.Shots.AddAsync(shot, cancellationToken);

    public async Task UpdateMatchAfterShotAsync(
        string matchId,
        Guid? currentTurnPlayerId,
        Guid? winnerPlayerId,
        MatchStatus status,
        DateTimeOffset updatedAtUtc,
        DateTimeOffset? finishedAtUtc,
        CancellationToken cancellationToken) =>
        await dbContext.Matches
            .Where(match => match.Id == matchId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(match => match.CurrentTurnPlayerId, currentTurnPlayerId)
                    .SetProperty(match => match.WinnerPlayerId, winnerPlayerId)
                    .SetProperty(match => match.Status, status)
                    .SetProperty(match => match.UpdatedAtUtc, updatedAtUtc)
                    .SetProperty(match => match.FinishedAtUtc, finishedAtUtc),
                cancellationToken);

    public void RemovePlayer(Player player) => dbContext.Players.Remove(player);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await dbContext.SaveChangesAsync(cancellationToken);
}
