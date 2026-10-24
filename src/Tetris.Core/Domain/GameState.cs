namespace Tetris.Core.Domain;

public sealed class GameState
{
    public GameState(Board board, Piece currentPiece, Piece nextPiece, int score = 0, int totalLines = 0, GameStatus status = GameStatus.Running)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score));
        if (totalLines < 0) throw new ArgumentOutOfRangeException(nameof(totalLines));
        Board = board ?? throw new ArgumentNullException(nameof(board));
        CurrentPiece = currentPiece;
        NextPiece = nextPiece;
        Score = score;
        TotalLines = totalLines;
        Status = status;
    }

    public Board Board { get; }
    public Piece CurrentPiece { get; internal set; }
    public Piece NextPiece { get; internal set; }
    public int Score { get; internal set; }
    public int TotalLines { get; internal set; }
    public int Level => 1 + TotalLines / 10;
    public GameStatus Status { get; internal set; }
    public GameState Clone() => new(Board.Clone(), CurrentPiece, NextPiece, Score, TotalLines, Status);
}
