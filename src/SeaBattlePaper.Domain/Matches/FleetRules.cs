namespace SeaBattlePaper.Domain.Matches;

public static class FleetRules
{
    public const int BoardSize = 10;
    private static readonly int[] RequiredLengths = [4, 3, 3, 2, 2, 2, 1, 1, 1, 1];

    public static string? Validate(IReadOnlyCollection<FleetPlacement> fleet)
    {
        return Validate("classic", fleet);
    }

    public static string? Validate(string mode, IReadOnlyCollection<FleetPlacement> fleet)
    {
        var lengths = fleet.Select(ship => ship.Length).OrderDescending().ToArray();
        if (!lengths.SequenceEqual(RequiredLengths))
            return "The classic fleet must contain 1×4, 2×3, 3×2 and 4×1 ships.";

        return ValidatePartial(mode, fleet);
    }

    public static string? ValidatePartial(IReadOnlyCollection<FleetPlacement> fleet)
    {
        return ValidatePartial("classic", fleet);
    }

    public static string? ValidatePartial(string mode, IReadOnlyCollection<FleetPlacement> fleet)
    {
        var allowedCounts = RequiredLengths
            .GroupBy(length => length)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var group in fleet.GroupBy(ship => ship.Length))
        {
            if (!allowedCounts.TryGetValue(group.Key, out var allowed) || group.Count() > allowed)
                return "Fleet has too many ships of one length.";
        }

        var occupied = new HashSet<BoardCoordinate>();
        foreach (var ship in fleet)
        {
            var shapeError = ValidateShape(mode, ship);
            if (shapeError is not null) return shapeError;

            var cells = ship.Cells().ToArray();
            if (cells.Any(cell => !IsInside(cell.Row, cell.Column)))
                return "Every ship must be inside the 10×10 board.";

            if (cells.Any(cell => occupied.Contains(cell)))
                return "Ships cannot overlap.";

            foreach (var cell in cells)
                for (var rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    for (var columnOffset = -1; columnOffset <= 1; columnOffset++)
                        if (occupied.Contains(new BoardCoordinate(
                                cell.Row + rowOffset,
                                cell.Column + columnOffset)))
                            return "Ships cannot touch, including at corners.";
                }

            foreach (var cell in cells)
                occupied.Add(cell);
        }

        return null;
    }

    public static bool IsInside(int row, int column) =>
        row is >= 0 and < BoardSize && column is >= 0 and < BoardSize;

    private static string? ValidateShape(string mode, FleetPlacement ship)
    {
        var offsets = Normalize(ship.GetOffsets()).ToArray();
        if (offsets.Length != ship.Length) return "Ship shape must match ship length.";
        if (offsets.Distinct().Count() != offsets.Length) return "Ship shape cannot contain duplicate cells.";
        if (!IsConnected(offsets)) return "Ship shape must be connected.";

        if (string.Equals(mode, "classic", StringComparison.OrdinalIgnoreCase) && !IsStraight(offsets))
            return "Classic ships must be straight.";

        return null;
    }

    private static bool IsStraight(IReadOnlyCollection<BoardCoordinate> offsets) =>
        offsets.Select(offset => offset.Row).Distinct().Count() == 1
        || offsets.Select(offset => offset.Column).Distinct().Count() == 1;

    private static bool IsConnected(IReadOnlyCollection<BoardCoordinate> offsets)
    {
        var remaining = offsets.ToHashSet();
        var queue = new Queue<BoardCoordinate>();
        var first = remaining.First();
        queue.Enqueue(first);
        remaining.Remove(first);

        while (queue.TryDequeue(out var cell))
        {
            foreach (var neighbor in Neighbors(cell).Where(remaining.Contains))
            {
                remaining.Remove(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return remaining.Count == 0;

        static IEnumerable<BoardCoordinate> Neighbors(BoardCoordinate cell)
        {
            yield return new BoardCoordinate(cell.Row - 1, cell.Column);
            yield return new BoardCoordinate(cell.Row + 1, cell.Column);
            yield return new BoardCoordinate(cell.Row, cell.Column - 1);
            yield return new BoardCoordinate(cell.Row, cell.Column + 1);
        }
    }

    private static IEnumerable<BoardCoordinate> Normalize(IEnumerable<BoardCoordinate> offsets)
    {
        var items = offsets.ToArray();
        var minRow = items.Min(offset => offset.Row);
        var minColumn = items.Min(offset => offset.Column);

        return items
            .Select(offset => new BoardCoordinate(offset.Row - minRow, offset.Column - minColumn))
            .OrderBy(offset => offset.Row)
            .ThenBy(offset => offset.Column);
    }
}
