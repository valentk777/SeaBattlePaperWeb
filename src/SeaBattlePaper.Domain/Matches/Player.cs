namespace SeaBattlePaper.Domain.Matches;

public sealed class Player
{
    public Guid Id { get; set; }

    public required string MatchId { get; set; }

    public int Seat { get; set; }

    public required string Nickname { get; set; }

    public required string TokenHash { get; set; }

    public bool IsReady { get; set; }

    public DateTimeOffset JoinedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset? ReadyAtUtc { get; set; }

    public Match Match { get; set; } = null!;

    public List<Ship> Ships { get; set; } = [];
}
