namespace SeaBattlePaper.Domain.Matches;

public sealed class Ship
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public int Length { get; set; }

    public int Row { get; set; }

    public int Column { get; set; }

    public bool IsHorizontal { get; set; }

    public string CellOffsets { get; set; } = string.Empty;

    public Player Player { get; set; } = null!;

    public IEnumerable<BoardCoordinate> Cells()
    {
        foreach (var offset in GetOffsets())
            yield return new BoardCoordinate(Row + offset.Row, Column + offset.Column);
    }

    public IReadOnlyCollection<BoardCoordinate> GetOffsets()
    {
        if (!string.IsNullOrWhiteSpace(CellOffsets))
            return Normalize(CellOffsets
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item =>
                {
                    var parts = item.Split(':');
                    return new BoardCoordinate(int.Parse(parts[0]), int.Parse(parts[1]));
                }));

        return Enumerable
            .Range(0, Length)
            .Select(offset => new BoardCoordinate(IsHorizontal ? 0 : offset, IsHorizontal ? offset : 0))
            .ToArray();
    }

    private static BoardCoordinate[] Normalize(IEnumerable<BoardCoordinate> offsets)
    {
        var items = offsets.ToArray();
        var minRow = items.Min(offset => offset.Row);
        var minColumn = items.Min(offset => offset.Column);

        return items
            .Select(offset => new BoardCoordinate(offset.Row - minRow, offset.Column - minColumn))
            .OrderBy(offset => offset.Row)
            .ThenBy(offset => offset.Column)
            .ToArray();
    }
}
