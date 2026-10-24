using Tetris.Core.Domain;

namespace Tetris.Tests;

public sealed class PieceTests
{
    [Theory]
    [InlineData(TetrominoType.I)]
    [InlineData(TetrominoType.O)]
    [InlineData(TetrominoType.T)]
    [InlineData(TetrominoType.S)]
    [InlineData(TetrominoType.Z)]
    [InlineData(TetrominoType.J)]
    [InlineData(TetrominoType.L)]
    public void Rotation_PreservesFourUniqueCells(TetrominoType type)
    {
        var piece = new Piece(type, 0, 0);
        var rotated = piece.RotatedClockwise();

        Assert.Equal(4, rotated.GetRelativeCells().Count);
        Assert.Equal(4, rotated.GetRelativeCells().Distinct().Count());
        Assert.Equal(1, rotated.Rotation);
        Assert.Equal(0, rotated.GetRelativeCells().Min(point => point.X));
        Assert.Equal(0, rotated.GetRelativeCells().Min(point => point.Y));
    }
}
