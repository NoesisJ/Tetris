namespace Tetris.Core.Domain;

public sealed class Board
{
    private readonly TetrominoType?[,] _cells;

    public Board(int width = 12, int height = 16)
    {
        if (width < 4) throw new ArgumentOutOfRangeException(nameof(width), "棋盘宽度至少为 4。");
        if (height < 4) throw new ArgumentOutOfRangeException(nameof(height), "棋盘高度至少为 4。");
        Width = width;
        Height = height;
        _cells = new TetrominoType?[width, height];
    }

    private Board(TetrominoType?[,] cells)
    {
        _cells = cells;
        Width = cells.GetLength(0);
        Height = cells.GetLength(1);
    }

    public int Width { get; }
    public int Height { get; }

    public TetrominoType? GetCell(int x, int y)
    {
        ValidateCoordinates(x, y);
        return _cells[x, y];
    }

    public void SetCell(int x, int y, TetrominoType? value)
    {
        ValidateCoordinates(x, y);
        _cells[x, y] = value;
    }

    public bool CanPlace(Piece piece)
    {
        foreach (var cell in piece.GetCells())
        {
            if (cell.X < 0 || cell.X >= Width || cell.Y >= Height) return false;
            if (cell.Y >= 0 && _cells[cell.X, cell.Y].HasValue) return false;
        }

        return true;
    }

    public void Place(Piece piece)
    {
        if (!CanPlace(piece)) throw new InvalidOperationException("不能将方块放置在该位置。");
        foreach (var cell in piece.GetCells())
        {
            if (cell.Y < 0) throw new InvalidOperationException("方块仍在棋盘顶部之外，不能锁定。");
            _cells[cell.X, cell.Y] = piece.Type;
        }
    }

    public int ClearFullRows()
    {
        var destinationY = Height - 1;
        var cleared = 0;
        for (var sourceY = Height - 1; sourceY >= 0; sourceY--)
        {
            var full = true;
            for (var x = 0; x < Width; x++)
            {
                if (_cells[x, sourceY].HasValue) continue;
                full = false;
                break;
            }

            if (full)
            {
                cleared++;
                continue;
            }

            if (destinationY != sourceY)
            {
                for (var x = 0; x < Width; x++) _cells[x, destinationY] = _cells[x, sourceY];
            }
            destinationY--;
        }

        while (destinationY >= 0)
        {
            for (var x = 0; x < Width; x++) _cells[x, destinationY] = null;
            destinationY--;
        }

        return cleared;
    }

    public bool AddGarbageRows(int count, int holeX)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if ((uint)holeX >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(holeX));
        var overflow = false;
        for (var row = 0; row < count; row++)
        {
            for (var x = 0; x < Width; x++)
                if (_cells[x, 0].HasValue) overflow = true;

            for (var y = 0; y < Height - 1; y++)
                for (var x = 0; x < Width; x++)
                    _cells[x, y] = _cells[x, y + 1];

            for (var x = 0; x < Width; x++)
                _cells[x, Height - 1] = x == holeX ? null : TetrominoType.Z;
        }

        return overflow;
    }

    public Board Clone()
    {
        var copy = new TetrominoType?[Width, Height];
        Array.Copy(_cells, copy, _cells.Length);
        return new Board(copy);
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
    }
}
