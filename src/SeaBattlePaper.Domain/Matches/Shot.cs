namespace SeaBattlePaper.Domain.Matches;

public sealed class Shot
{
    public Guid Id { get; set; }

    public required string MatchId { get; set; }

    public Guid AttackerPlayerId { get; set; }

    public Guid DefenderPlayerId { get; set; }

    public int Row { get; set; }

    public int Column { get; set; }

    public ShotResult Result { get; set; }

    public int Sequence { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
