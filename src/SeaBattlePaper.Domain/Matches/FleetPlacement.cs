namespace SeaBattlePaper.Domain.Matches;

public sealed record FleetPlacement(
    int Length,
    int StartRow,
    int StartColumn,
    bool IsHorizontal,
    IReadOnlyCollection<BoardCoordinate>? CellOffsets = null)
{
    public IEnumerable<BoardCoordinate> Cells()
    {
        foreach (var offset in GetOffsets())
            yield return new BoardCoordinate(StartRow + offset.Row, StartColumn + offset.Column);
    }

    public IReadOnlyCollection<BoardCoordinate> GetOffsets()
    {
        if (CellOffsets is { Count: > 0 }) return Normalize(CellOffsets);

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
