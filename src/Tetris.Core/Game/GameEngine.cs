using Tetris.Core.AI;
using Tetris.Core.Domain;

namespace Tetris.Core.Game;

public sealed class GameEngine
{
    public const int BaseDropIntervalMilliseconds = 800;
    public const int DropIntervalStepMilliseconds = 70;
    public const int MinimumDropIntervalMilliseconds = 100;
    private readonly IGamePieceSource _pieceSource;
    private readonly IScoringRule _scoringRule;

    public GameEngine(IGamePieceSource? pieceSource = null, GameState? initialState = null, IScoringRule? scoringRule = null)
    {
        _pieceSource = pieceSource ?? new RandomPieceSource();
        _scoringRule = scoringRule ?? new StandardScoringRule();
        State = initialState ?? CreateInitialState(new Board(), _pieceSource);
        if (!State.Board.CanPlace(State.CurrentPiece)) State.Status = GameStatus.GameOver;
    }

    public GameState State { get; private set; }
    public int LastClearedRows { get; private set; }

    public int ConsumeLastClearedRows()
    {
        var cleared = LastClearedRows;
        LastClearedRows = 0;
        return cleared;
    }

    public static GameState CreateInitialState(Board board, IGamePieceSource source)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(source);
        var currentType = source.NextType();
        var nextType = source.NextType();
        return new GameState(board, Spawn(currentType, board), Spawn(nextType, board));
    }

    public int DropIntervalMilliseconds => Math.Max(
        MinimumDropIntervalMilliseconds,
        BaseDropIntervalMilliseconds - (State.Level - 1) * DropIntervalStepMilliseconds);

    public bool MoveHorizontal(int direction)
    {
        if (State.Status != GameStatus.Running || direction == 0) return false;
        var candidate = State.CurrentPiece with { X = State.CurrentPiece.X + Math.Sign(direction) };
        if (!State.Board.CanPlace(candidate)) return false;
        State.CurrentPiece = candidate;
        return true;
    }

    public bool RotateClockwise()
    {
        if (State.Status != GameStatus.Running) return false;
        var rotated = State.CurrentPiece.RotatedClockwise();
        foreach (var kick in new[] { 0, -1, 1, -2, 2 })
        {
            var candidate = rotated with { X = rotated.X + kick };
            if (!State.Board.CanPlace(candidate)) continue;
            State.CurrentPiece = candidate;
            return true;
        }

        return false;
    }

    public void TogglePause()
    {
        State.Status = State.Status switch
        {
            GameStatus.Running => GameStatus.Paused,
            GameStatus.Paused => GameStatus.Running,
            _ => GameStatus.GameOver
        };
    }

    public void Tick()
    {
        LastClearedRows = 0;
        if (State.Status != GameStatus.Running) return;
        var candidate = State.CurrentPiece with { Y = State.CurrentPiece.Y + 1 };
        if (State.Board.CanPlace(candidate))
        {
            State.CurrentPiece = candidate;
            return;
        }

        LockCurrentPiece();
    }

    public void HardDrop()
    {
        LastClearedRows = 0;
        if (State.Status != GameStatus.Running) return;
        while (State.Board.CanPlace(State.CurrentPiece with { Y = State.CurrentPiece.Y + 1 }))
            State.CurrentPiece = State.CurrentPiece with { Y = State.CurrentPiece.Y + 1 };
        LockCurrentPiece();
    }

    public bool ApplyAiMove(AiMove move)
    {
        if (State.Status != GameStatus.Running) return false;
        var candidate = State.CurrentPiece with { X = move.X, Rotation = move.Rotation, Y = State.CurrentPiece.Y };
        if (!State.Board.CanPlace(candidate)) return false;
        State.CurrentPiece = candidate;
        HardDrop();
        return true;
    }

    public void ReceiveGarbageRows(int count, int holeX)
    {
        if (State.Status != GameStatus.Running || count <= 0) return;
        if (State.Board.AddGarbageRows(count, holeX)) State.Status = GameStatus.GameOver;
        else if (!State.Board.CanPlace(State.CurrentPiece)) State.Status = GameStatus.GameOver;
    }

    public void Restart()
    {
        State = CreateInitialState(new Board(), _pieceSource);
        if (!State.Board.CanPlace(State.CurrentPiece)) State.Status = GameStatus.GameOver;
    }

    private void LockCurrentPiece()
    {
        if (State.CurrentPiece.GetCells().Any(cell => cell.Y < 0))
        {
            State.Status = GameStatus.GameOver;
            return;
        }

        State.Board.Place(State.CurrentPiece);
        var cleared = State.Board.ClearFullRows();
        LastClearedRows = cleared;
        State.TotalLines += cleared;
        State.Score = checked(State.Score + _scoringRule.CalculateScore(cleared, State.Level));
        State.CurrentPiece = Spawn(State.NextPiece.Type, State.Board);
        State.NextPiece = Spawn(_pieceSource.NextType(), State.Board);
        if (!State.Board.CanPlace(State.CurrentPiece)) State.Status = GameStatus.GameOver;
    }

    private static Piece Spawn(TetrominoType type, Board board)
    {
        var piece = new Piece(type, 0, 0);
        return piece with { X = (board.Width - piece.Width) / 2 };
    }
}
