namespace Tetris.Core.Domain;

public readonly record struct Piece(TetrominoType Type, int X, int Y, int Rotation = 0)
{
    private static readonly IReadOnlyDictionary<TetrominoType, GridPoint[]> Shapes =
        new Dictionary<TetrominoType, GridPoint[]>
        {
            [TetrominoType.I] = [new(0, 0), new(1, 0), new(2, 0), new(3, 0)],
            [TetrominoType.O] = [new(0, 0), new(1, 0), new(0, 1), new(1, 1)],
            [TetrominoType.T] = [new(1, 0), new(0, 1), new(1, 1), new(2, 1)],
            [TetrominoType.S] = [new(1, 0), new(2, 0), new(0, 1), new(1, 1)],
            [TetrominoType.Z] = [new(0, 0), new(1, 0), new(1, 1), new(2, 1)],
            [TetrominoType.J] = [new(0, 0), new(0, 1), new(1, 1), new(2, 1)],
            [TetrominoType.L] = [new(2, 0), new(0, 1), new(1, 1), new(2, 1)]
        };

    public IReadOnlyList<GridPoint> GetRelativeCells()
    {
        var cells = Shapes[Type].ToArray();
        for (var turn = 0; turn < ((Rotation % 4) + 4) % 4; turn++)
        {
            cells = cells.Select(point => new GridPoint(3 - point.Y, point.X)).ToArray();
            var minX = cells.Min(point => point.X);
            var minY = cells.Min(point => point.Y);
            cells = cells.Select(point => new GridPoint(point.X - minX, point.Y - minY)).ToArray();
        }

        return cells;
    }

    public IReadOnlyList<GridPoint> GetCells()
    {
        var originX = X;
        var originY = Y;
        return GetRelativeCells().Select(point => new GridPoint(originX + point.X, originY + point.Y)).ToArray();
    }

    public int Width => GetRelativeCells().Max(point => point.X) + 1;
    public int Height => GetRelativeCells().Max(point => point.Y) + 1;

    public Piece RotatedClockwise() => this with { Rotation = (Rotation + 1) % 4 };
}
