using SeaBattlePaper.Domain.Matches;

namespace SeaBattlePaper.Contracts.Matches;

public sealed record CreateMatchRequest(string Mode, bool RevealSunkShips = true);

public sealed record CreateMatchResponse(string MatchId, string PlayerToken, int Seat);

public sealed record ValidateMatchResponse(string MatchId, bool Exists);

public sealed record ShipCellOffsetDto(int Row, int Column);

public sealed record FleetPlacementDto(
    int Row,
    int Column,
    int Length,
    bool IsHorizontal,
    IReadOnlyCollection<ShipCellOffsetDto>? CellOffsets = null);

public sealed record JoinMatchResponse(string MatchId, string PlayerToken, int Seat);

public sealed record PlayerStateDto(Guid Id, int Seat, string Nickname, bool IsReady, bool IsOnline);

public sealed record CellStateDto(int Row, int Column, bool HasShip, ShotResult? ShotResult, int? ShotSequence);

public sealed record ShipStateDto(
    int Row,
    int Column,
    int Length,
    bool IsHorizontal,
    bool IsSunk,
    IReadOnlyCollection<ShipCellOffsetDto> CellOffsets);

public sealed record MatchStateDto(
    string MatchId,
    string Mode,
    bool RevealSunkShips,
    MatchStatus Status,
    Guid ViewerPlayerId,
    int ViewerSeat,
    Guid? CurrentTurnPlayerId,
    Guid? WinnerPlayerId,
    IReadOnlyCollection<PlayerStateDto> Players,
    IReadOnlyCollection<CellStateDto> MyBoard,
    IReadOnlyCollection<CellStateDto> OpponentBoard,
    IReadOnlyCollection<ShipStateDto> MyShips,
    IReadOnlyCollection<ShipStateDto> OpponentShips,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);
