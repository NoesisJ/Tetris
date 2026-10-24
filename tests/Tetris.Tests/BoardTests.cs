using Tetris.Core.Domain;

namespace Tetris.Tests;

public sealed class BoardTests
{
    [Fact]
    public void CanPlace_RejectsWallAndOccupiedCells()
    {
        var board = new Board();
        Assert.False(board.CanPlace(new Piece(TetrominoType.I, -1, 0)));
        board.SetCell(5, 5, TetrominoType.T);
        Assert.False(board.CanPlace(new Piece(TetrominoType.O, 5, 4)));
    }

    [Fact]
    public void ClearFullRows_RemovesFullRowAndMovesRowsAboveDown()
    {
        var board = new Board(width: 6, height: 6);
        board.SetCell(0, 3, TetrominoType.T);
        for (var x = 0; x < board.Width; x++)
            if (x != 2) board.SetCell(x, 5, TetrominoType.I);
        board.SetCell(2, 5, TetrominoType.O);

        Assert.Equal(1, board.ClearFullRows());
        Assert.Equal(TetrominoType.T, board.GetCell(0, 4));
        Assert.Null(board.GetCell(0, 5));
        Assert.Null(board.GetCell(2, 5));
    }

    [Fact]
    public void Clone_CopiesCellsIndependently()
    {
        var original = new Board();
        original.SetCell(2, 3, TetrominoType.L);
        var copy = original.Clone();
        copy.SetCell(2, 3, null);

        Assert.Equal(TetrominoType.L, original.GetCell(2, 3));
        Assert.Null(copy.GetCell(2, 3));
    }

    [Fact]
    public void AddGarbageRows_FillsRowsExceptHoleAndShiftsBoardUp()
    {
        var board = new Board(width: 6, height: 6);
        board.SetCell(2, 4, TetrominoType.T);

        var overflow = board.AddGarbageRows(count: 2, holeX: 3);

        Assert.False(overflow);
        Assert.Equal(TetrominoType.T, board.GetCell(2, 2));
        Assert.Null(board.GetCell(3, 4));
        Assert.Null(board.GetCell(3, 5));
        Assert.Equal(5, Enumerable.Range(0, board.Width).Count(x => board.GetCell(x, 4).HasValue));
        Assert.Equal(5, Enumerable.Range(0, board.Width).Count(x => board.GetCell(x, 5).HasValue));
    }

    [Fact]
    public void AddGarbageRows_ReportsOverflowWhenTopRowIsDiscarded()
    {
        var board = new Board(width: 6, height: 6);
        board.SetCell(1, 0, TetrominoType.I);

        Assert.True(board.AddGarbageRows(1, holeX: 0));
    }
}
