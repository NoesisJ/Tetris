using Tetris.Core.AI;
using Tetris.Core.Domain;

namespace Tetris.Tests;

public sealed class AiPlannerTests
{
    [Fact]
    public void FindBestMove_ReturnsLegalLandingPosition()
    {
        var board = new Board();
        for (var y = 12; y < 16; y++) board.SetCell(0, y, TetrominoType.J);
        var piece = new Piece(TetrominoType.T, 4, 0);

        var move = new AiPlanner().FindBestMove(board, piece);
        var candidate = piece with { X = move.X, Rotation = move.Rotation };
        Assert.InRange(move.Rotation, 0, 3);
        Assert.True(board.CanPlace(candidate));
        while (board.CanPlace(candidate with { Y = candidate.Y + 1 })) candidate = candidate with { Y = candidate.Y + 1 };
        Assert.False(board.CanPlace(candidate with { Y = candidate.Y + 1 }));
    }
}
