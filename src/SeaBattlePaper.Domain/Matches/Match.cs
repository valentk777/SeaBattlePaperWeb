namespace SeaBattlePaper.Domain.Matches;

public sealed class Match
{
    public required string Id { get; set; }

    public string Mode { get; set; } = "classic";

    public bool RevealSunkShips { get; set; } = true;

    public MatchStatus Status { get; set; } = MatchStatus.Lobby;

    public Guid? CurrentTurnPlayerId { get; set; }

    public Guid? WinnerPlayerId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public List<Player> Players { get; set; } = [];

    public List<Shot> Shots { get; set; } = [];
}
