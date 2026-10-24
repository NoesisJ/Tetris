using Tetris.Core.Domain;

namespace Tetris.Core.AI;

public sealed class AiPlanner
{
    public AiMove FindBestMove(Board board, Piece piece)
    {
        ArgumentNullException.ThrowIfNull(board);
        var found = false;
        var bestScore = double.PositiveInfinity;
        var bestMove = new AiMove(piece.Rotation, piece.X);

        for (var rotation = 0; rotation < 4; rotation++)
        {
            var rotated = piece with { Rotation = rotation };
            for (var x = 0; x <= board.Width - rotated.Width; x++)
            {
                var candidate = rotated with { X = x };
                if (!board.CanPlace(candidate)) continue;
                while (board.CanPlace(candidate with { Y = candidate.Y + 1 }))
                    candidate = candidate with { Y = candidate.Y + 1 };

                var simulation = board.Clone();
                simulation.Place(candidate);
                var clearedRows = simulation.ClearFullRows();
                var score = Evaluate(simulation, clearedRows);
                if (score >= bestScore) continue;
                bestScore = score;
                bestMove = new AiMove(rotation, x);
                found = true;
            }
        }

        if (!found) throw new InvalidOperationException("当前方块没有可用落点。");
        return bestMove;
    }

    private static double Evaluate(Board board, int clearedRows)
    {
        var heights = new int[board.Width];
        var holes = 0;
        for (var x = 0; x < board.Width; x++)
        {
            var foundBlock = false;
            for (var y = 0; y < board.Height; y++)
            {
                if (board.GetCell(x, y).HasValue)
                {
                    if (!foundBlock)
                    {
                        heights[x] = board.Height - y;
                        foundBlock = true;
                    }
                }
                else if (foundBlock)
                {
                    holes++;
                }
            }
        }

        var aggregateHeight = heights.Sum();
        var bumpiness = 0;
        for (var x = 0; x < heights.Length - 1; x++) bumpiness += Math.Abs(heights[x] - heights[x + 1]);
        return aggregateHeight * 0.51 + holes * 0.36 + bumpiness * 0.18 - clearedRows * 0.76;
    }
}
