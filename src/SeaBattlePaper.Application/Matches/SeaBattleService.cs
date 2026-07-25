using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SeaBattlePaper.Application.Common;
using SeaBattlePaper.Contracts.Matches;
using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Application.Matches;

public sealed class SeaBattleService(ISeaBattleStore store, TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MatchLocks = new();

    public async Task<OperationResult<CreateMatchResponse>> CreateMatchAsync(
        CreateMatchRequest request,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode.Trim().ToLowerInvariant();
        if (mode is not ("classic" or "paper"))
            return OperationResult<CreateMatchResponse>.Failure("unsupported_mode", "Only classic and paper modes are available.");

        var matchId = await CreateUniqueMatchIdAsync(cancellationToken);
        var token = CreateToken();
        var now = GetUtcNow();
        var match = new Match
        {
            Id = matchId,
            Mode = mode,
            RevealSunkShips = request.RevealSunkShips,
            Status = MatchStatus.Lobby,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        match.Players.Add(new Player
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            Seat = 1,
            Nickname = "Player 1",
            TokenHash = HashToken(token),
            JoinedAtUtc = now,
            LastSeenAtUtc = now
        });

        await store.AddMatchAsync(match, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        return OperationResult<CreateMatchResponse>.Success(new CreateMatchResponse(matchId, token, 1));
    }

    public async Task<OperationResult<bool>> ValidateMatchAsync(string matchId, CancellationToken cancellationToken)
    {
        var exists = await store.MatchExistsAsync(matchId, cancellationToken);

        return OperationResult<bool>.Success(exists);
    }

    public async Task<OperationResult<MatchAccess>> JoinMatchAsync(
        string matchId,
        string? playerToken,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var match = await store.GetMatchForJoinAsync(matchId, cancellationToken);
            if (match is null) return OperationResult<MatchAccess>.Failure("match_not_found", "This match does not exist.");

            var now = GetUtcNow();
            var token = string.IsNullOrWhiteSpace(playerToken) ? null : playerToken.Trim();
            var existing = token is null ? null : match.Players.FirstOrDefault(player => TokenMatches(token, player.TokenHash));
            if (existing is not null)
            {
                existing.LastSeenAtUtc = now;
                await store.UpdatePlayerLastSeenAsync(existing.Id, now, cancellationToken);
                await store.SaveChangesAsync(cancellationToken);

                return OperationResult<MatchAccess>.Success(new MatchAccess(match, existing, token!));
            }

            if (match.Players.Count >= 2 || match.Status != MatchStatus.Lobby)
                return OperationResult<MatchAccess>.Failure("match_full", "This match already has two players.");

            var newToken = CreateToken();
            var player = new Player
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                Seat = 2,
                Nickname = "Player 2",
                TokenHash = HashToken(newToken),
                JoinedAtUtc = now,
                LastSeenAtUtc = now
            };

            await store.AddPlayerAsync(player, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            match.Players.Add(player);

            return OperationResult<MatchAccess>.Success(new MatchAccess(match, player, newToken));
        });

    public async Task<OperationResult> UpdateNicknameAsync(
        string matchId,
        Guid playerId,
        string nickname,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var access = await GetPlayerAccessAsync(matchId, playerId, cancellationToken);
            if (!access.IsSuccess) return OperationResult.Failure(access.Error!.Code, access.Error.Message);

            var match = access.Value!.Match;
            var player = access.Value.Player;
            var trimmed = nickname.Trim();
            if (trimmed.Length is < 1 or > 24) return OperationResult.Failure("invalid_nickname", "Nickname must be between 1 and 24 characters.");

            if (player.IsReady) return OperationResult.Failure("nickname_locked", "Nickname cannot be changed after ready.");

            if (match.Players.Any(other =>
                    other.Id != player.Id && string.Equals(other.Nickname, trimmed, StringComparison.OrdinalIgnoreCase)))
                return OperationResult.Failure("nickname_taken", "This nickname is already used in the match.");

            player.Nickname = trimmed;
            player.LastSeenAtUtc = GetUtcNow();
            match.UpdatedAtUtc = player.LastSeenAtUtc;
            await store.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        });

    public async Task<OperationResult> ReadyUpAsync(
        string matchId,
        Guid playerId,
        IReadOnlyCollection<FleetPlacementDto> fleet,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var match = await store.GetMatchForJoinAsync(matchId, cancellationToken);
            if (match is null) return OperationResult.Failure("match_not_found", "This match does not exist.");

            var player = match.Players.FirstOrDefault(item => item.Id == playerId);
            if (player is null) return OperationResult.Failure("player_not_found", "You are not part of this match.");

            if (match.Status != MatchStatus.Lobby) return OperationResult.Failure("match_started", "Ship placement is closed.");

            var placements = fleet.Select(ToPlacement).ToArray();
            var validationError = FleetRules.Validate(match.Mode, placements);
            if (validationError is not null) return OperationResult.Failure("invalid_fleet", validationError);

            var readyAtUtc = GetUtcNow();
            var ships = placements.Select(placement => ToShip(player.Id, placement)).ToArray();

            await store.ReplacePlayerFleetAndReadyAsync(player.Id, ships, readyAtUtc, cancellationToken);

            player.IsReady = true;
            if (match.Players.Count == 2 && match.Players.All(item => item.IsReady))
            {
                var firstPlayerId = match.Players[RandomNumberGenerator.GetInt32(match.Players.Count)].Id;
                await store.StartMatchAsync(match.Id, firstPlayerId, readyAtUtc, cancellationToken);
            }

            await store.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        });

    public async Task<OperationResult> SaveFleetDraftAsync(
        string matchId,
        Guid playerId,
        IReadOnlyCollection<FleetPlacementDto> fleet,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var match = await store.GetMatchForJoinAsync(matchId, cancellationToken);
            if (match is null) return OperationResult.Failure("match_not_found", "This match does not exist.");

            var player = match.Players.FirstOrDefault(item => item.Id == playerId);
            if (player is null) return OperationResult.Failure("player_not_found", "You are not part of this match.");

            if (match.Status != MatchStatus.Lobby) return OperationResult.Failure("match_started", "Ship placement is closed.");
            if (player.IsReady) return OperationResult.Failure("fleet_locked", "Fleet cannot be changed after ready.");

            var placements = fleet.Select(ToPlacement).ToArray();
            var validationError = FleetRules.ValidatePartial(match.Mode, placements);
            if (validationError is not null) return OperationResult.Failure("invalid_fleet", validationError);

            var updatedAtUtc = GetUtcNow();
            var ships = placements.Select(placement => ToShip(player.Id, placement)).ToArray();

            await store.ReplacePlayerFleetAsync(player.Id, ships, updatedAtUtc, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        });

    public async Task<OperationResult<IReadOnlyCollection<Guid>>> RemoveOpponentAsync(
        string matchId,
        Guid playerId,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var access = await GetPlayerAccessAsync(matchId, playerId, cancellationToken);
            if (!access.IsSuccess) return OperationResult<IReadOnlyCollection<Guid>>.Failure(access.Error!.Code, access.Error.Message);

            var match = access.Value!.Match;
            var player = access.Value.Player;
            if (match.Status != MatchStatus.Lobby || player.Seat != 1)
                return OperationResult<IReadOnlyCollection<Guid>>.Failure(
                    "remove_not_allowed",
                    "Only the match creator can remove the opponent before the match starts.");

            var removed = match.Players.Where(item => item.Id != player.Id).ToArray();
            foreach (var removedPlayer in removed)
            {
                store.RemovePlayer(removedPlayer);
                match.Players.Remove(removedPlayer);
            }

            match.UpdatedAtUtc = GetUtcNow();
            await store.SaveChangesAsync(cancellationToken);

            return OperationResult<IReadOnlyCollection<Guid>>.Success(removed.Select(item => item.Id).ToArray());
        });

    public async Task<OperationResult> FireAsync(
        string matchId,
        Guid playerId,
        int row,
        int column,
        CancellationToken cancellationToken) =>
        await WithMatchLockAsync(matchId, async () =>
        {
            var access = await GetPlayerAccessAsync(matchId, playerId, cancellationToken);
            if (!access.IsSuccess) return OperationResult.Failure(access.Error!.Code, access.Error.Message);

            var match = access.Value!.Match;
            var attacker = access.Value.Player;
            if (match.Status != MatchStatus.InProgress) return OperationResult.Failure("match_not_running", "This match is not running.");

            if (match.CurrentTurnPlayerId != attacker.Id) return OperationResult.Failure("not_your_turn", "It is not your turn.");

            if (row is < 0 or >= FleetRules.BoardSize || column is < 0 or >= FleetRules.BoardSize)
                return OperationResult.Failure("invalid_coordinate", "Shot is outside the board.");

            var defender = match.Players.Single(player => player.Id != attacker.Id);
            if (match.Shots.Any(shot => shot.AttackerPlayerId == attacker.Id && shot.Row == row && shot.Column == column))
                return OperationResult.Failure("duplicate_shot", "This cell was already fired at.");

            var target = new BoardCoordinate(row, column);
            var hitShip = defender.Ships.FirstOrDefault(ship => ship.Cells().Contains(target));
            var result = ShotResult.Miss;
            if (hitShip is not null)
            {
                var previousHits = match.Shots
                    .Where(shot => shot.AttackerPlayerId == attacker.Id && shot.Result != ShotResult.Miss)
                    .Select(shot => new BoardCoordinate(shot.Row, shot.Column))
                    .Append(target)
                    .ToHashSet();
                result = hitShip.Cells().All(previousHits.Contains) ? ShotResult.Sunk : ShotResult.Hit;
            }

            var sequence = match.Shots.Count == 0 ? 1 : match.Shots.Max(shot => shot.Sequence) + 1;
            var shot = new Shot
            {
                Id = Guid.NewGuid(),
                MatchId = match.Id,
                AttackerPlayerId = attacker.Id,
                DefenderPlayerId = defender.Id,
                Row = row,
                Column = column,
                Result = result,
                Sequence = sequence,
                CreatedAtUtc = GetUtcNow()
            };

            Guid? currentTurnPlayerId = result == ShotResult.Miss ? defender.Id : attacker.Id;
            var winnerPlayerId = match.WinnerPlayerId;
            var status = match.Status;
            DateTimeOffset? finishedAtUtc = match.FinishedAtUtc;

            if (HasLost(defender, match.Shots.Where(existingShot => existingShot.AttackerPlayerId == attacker.Id).Append(shot)))
            {
                status = MatchStatus.Finished;
                winnerPlayerId = attacker.Id;
                finishedAtUtc = GetUtcNow();
                currentTurnPlayerId = null;
            }

            await store.AddShotAsync(shot, cancellationToken);
            await store.UpdateMatchAfterShotAsync(
                match.Id,
                currentTurnPlayerId,
                winnerPlayerId,
                status,
                GetUtcNow(),
                finishedAtUtc,
                cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            return OperationResult.Success();
        });

    public async Task<OperationResult<MatchStateDto>> GetStateAsync(
        string matchId,
        Guid viewerPlayerId,
        IReadOnlySet<Guid> onlinePlayers,
        CancellationToken cancellationToken)
    {
        var match = await store.GetMatchSnapshotAsync(matchId, cancellationToken);
        if (match is null)
            return OperationResult<MatchStateDto>.Failure("match_not_found", "This match does not exist.");

        var player = match.Players.FirstOrDefault(item => item.Id == viewerPlayerId);
        if (player is null)
            return OperationResult<MatchStateDto>.Failure("player_not_found", "You are not part of this match.");

        return OperationResult<MatchStateDto>.Success(ProjectState(match, player, onlinePlayers));
    }

    private async Task<string> CreateUniqueMatchIdAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            var id = Convert.ToHexString(bytes).ToLowerInvariant();
            if (!await store.MatchExistsAsync(id, cancellationToken)) return id;
        }

        throw new InvalidOperationException("Could not generate a unique match id.");
    }

    private async Task<OperationResult<(Match Match, Player Player)>> GetPlayerAccessAsync(
        string matchId,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var match = await store.GetMatchAsync(matchId, cancellationToken);
        if (match is null) return OperationResult<(Match Match, Player Player)>.Failure("match_not_found", "This match does not exist.");

        var player = match.Players.FirstOrDefault(item => item.Id == playerId);
        if (player is null) return OperationResult<(Match Match, Player Player)>.Failure("player_not_found", "You are not part of this match.");

        return OperationResult<(Match Match, Player Player)>.Success((match, player));
    }

    private static async Task<T> WithMatchLockAsync<T>(string matchId, Func<Task<T>> action)
    {
        var matchLock = MatchLocks.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
        await matchLock.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            matchLock.Release();
        }
    }

    private static MatchStateDto ProjectState(
        Match match,
        Player viewer,
        IReadOnlySet<Guid> onlinePlayers)
    {
        var opponent = match.Players.FirstOrDefault(player => player.Id != viewer.Id);
        var viewerShots = match.Shots.Where(shot => shot.AttackerPlayerId == viewer.Id).ToArray();
        var opponentShots = match.Shots.Where(shot => shot.AttackerPlayerId == opponent?.Id).ToArray();
        var myBoard = BuildBoard(viewer, opponentShots, true);
        var opponentBoard = opponent is null
            ? Array.Empty<CellStateDto>()
            : BuildBoard(opponent, viewerShots, match.Status == MatchStatus.Finished, match.RevealSunkShips);

        return new MatchStateDto(
            match.Id,
            match.Mode,
            match.RevealSunkShips,
            match.Status,
            viewer.Id,
            viewer.Seat,
            match.CurrentTurnPlayerId,
            match.WinnerPlayerId,
            match.Players.OrderBy(player => player.Seat).Select(player => new PlayerStateDto(
                player.Id,
                player.Seat,
                player.Nickname,
                player.IsReady,
                onlinePlayers.Contains(player.Id))).ToArray(),
            myBoard,
            opponentBoard,
            ProjectShips(viewer, true, Array.Empty<Shot>()),
            opponent is null
                ? Array.Empty<ShipStateDto>()
                : ProjectShips(opponent, match.Status == MatchStatus.Finished, viewerShots),
            match.StartedAtUtc,
            match.FinishedAtUtc);
    }

    private static IReadOnlyCollection<CellStateDto> BuildBoard(
        Player owner,
        IReadOnlyCollection<Shot> incomingShots,
        bool revealAllShips,
        bool revealSunkShips = false)
    {
        var shotByCell = incomingShots.ToDictionary(shot => new BoardCoordinate(shot.Row, shot.Column));
        var visibleCells = new Dictionary<BoardCoordinate, CellStateDto>();
        var sunkShips = owner.Ships
            .Where(ship => IsShipSunk(ship, incomingShots))
            .ToArray();

        foreach (var shot in incomingShots)
        {
            var coordinate = new BoardCoordinate(shot.Row, shot.Column);
            var visibleResult = revealAllShips || revealSunkShips || shot.Result != ShotResult.Sunk
                ? shot.Result
                : ShotResult.Hit;
            visibleCells[coordinate] = new CellStateDto(shot.Row, shot.Column, false, visibleResult, shot.Sequence);
        }

        foreach (var ship in owner.Ships)
        {
            var shouldReveal = revealAllShips || (revealSunkShips && IsShipSunk(ship, incomingShots));
            if (!shouldReveal) continue;

            foreach (var cell in ship.Cells())
            {
                shotByCell.TryGetValue(cell, out var shot);
                visibleCells[cell] = new CellStateDto(cell.Row, cell.Column, true, shot?.Result, shot?.Sequence);
            }
        }

        if (revealSunkShips)
            foreach (var water in RevealWaterAround(sunkShips).Where(water => !visibleCells.ContainsKey(water)))
                visibleCells[water] = new CellStateDto(water.Row, water.Column, false, ShotResult.Miss, null);

        return visibleCells.Values.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray();
    }

    private static IReadOnlyCollection<ShipStateDto> ProjectShips(
        Player owner,
        bool reveal,
        IReadOnlyCollection<Shot> incomingShots)
    {
        if (!reveal) return Array.Empty<ShipStateDto>();

        return owner.Ships.Select(ship => new ShipStateDto(
            ship.Row,
            ship.Column,
            ship.Length,
            ship.IsHorizontal,
            IsShipSunk(ship, incomingShots),
            ship.GetOffsets()
                .Select(offset => new ShipCellOffsetDto(offset.Row, offset.Column))
                .ToArray())).ToArray();
    }

    private static IEnumerable<BoardCoordinate> RevealWaterAround(IEnumerable<Ship> ships)
    {
        foreach (var ship in ships)
        {
            foreach (var cell in ship.Cells())
                for (var row = cell.Row - 1; row <= cell.Row + 1; row++)
                {
                    for (var column = cell.Column - 1; column <= cell.Column + 1; column++)
                        if (row is >= 0 and < FleetRules.BoardSize && column is >= 0 and < FleetRules.BoardSize)
                            yield return new BoardCoordinate(row, column);
                }
        }
    }

    private static bool HasLost(Player defender, IEnumerable<Shot> attackerShots)
    {
        var hits = attackerShots
            .Where(shot => shot.Result != ShotResult.Miss)
            .Select(shot => new BoardCoordinate(shot.Row, shot.Column))
            .ToHashSet();

        return defender.Ships.SelectMany(ship => ship.Cells()).All(hits.Contains);
    }

    private static bool IsShipSunk(Ship ship, IReadOnlyCollection<Shot> incomingShots)
    {
        var hits = incomingShots
            .Where(shot => shot.Result != ShotResult.Miss)
            .Select(shot => new BoardCoordinate(shot.Row, shot.Column))
            .ToHashSet();

        return ship.Cells().All(hits.Contains);
    }

    private static string CreateToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static bool TokenMatches(string token, string tokenHash)
    {
        var computed = Encoding.UTF8.GetBytes(HashToken(token));
        var expected = Encoding.UTF8.GetBytes(tokenHash);

        return computed.Length == expected.Length && CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    private static FleetPlacement ToPlacement(FleetPlacementDto dto) =>
        new(
            dto.Length,
            dto.Row,
            dto.Column,
            dto.IsHorizontal,
            dto.CellOffsets?
                .Select(offset => new BoardCoordinate(offset.Row, offset.Column))
                .ToArray());

    private static Ship ToShip(Guid playerId, FleetPlacement placement) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Row = placement.StartRow,
            Column = placement.StartColumn,
            Length = placement.Length,
            IsHorizontal = placement.IsHorizontal,
            CellOffsets = SerializeOffsets(placement.GetOffsets())
        };

    private static string SerializeOffsets(IEnumerable<BoardCoordinate> offsets) =>
        string.Join(
            ';',
            NormalizeOffsets(offsets)
                .OrderBy(offset => offset.Row)
                .ThenBy(offset => offset.Column)
                .Select(offset => $"{offset.Row}:{offset.Column}"));

    private static IReadOnlyCollection<BoardCoordinate> NormalizeOffsets(IEnumerable<BoardCoordinate> offsets)
    {
        var items = offsets.ToArray();
        var minRow = items.Min(offset => offset.Row);
        var minColumn = items.Min(offset => offset.Column);

        return items
            .Select(offset => new BoardCoordinate(offset.Row - minRow, offset.Column - minColumn))
            .ToArray();
    }

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();
}
