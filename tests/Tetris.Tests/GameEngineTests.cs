using Tetris.Core.Domain;
using Tetris.Core.Game;

namespace Tetris.Tests;

public sealed class GameEngineTests
{
    [Fact]
    public void MoveHorizontal_StopsAtWall()
    {
        var engine = new GameEngine(new SequencePieceSource(),
            new GameState(new Board(), new Piece(TetrominoType.I, 0, 0), new Piece(TetrominoType.T, 4, 0)));

        Assert.False(engine.MoveHorizontal(-1));
        Assert.True(engine.MoveHorizontal(1));
        Assert.Equal(1, engine.State.CurrentPiece.X);
    }

    [Fact]
    public void RotateClockwise_LeavesPieceUnchangedWhenAllKicksCollide()
    {
        var board = new Board();
        for (var x = 0; x < board.Width; x++) board.SetCell(x, 6, TetrominoType.Z);
        var original = new Piece(TetrominoType.T, 4, 4);
        var engine = new GameEngine(new SequencePieceSource(), new GameState(board, original, new Piece(TetrominoType.I, 4, 0)));

        Assert.False(engine.RotateClockwise());
        Assert.Equal(original, engine.State.CurrentPiece);
    }

    [Fact]
    public void Tick_DoesNotAdvanceWhilePaused()
    {
        var engine = new GameEngine(new SequencePieceSource(),
            new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.I, 4, 0)));
        engine.TogglePause();
        engine.Tick();

        Assert.Equal(0, engine.State.CurrentPiece.Y);
        Assert.Equal(GameStatus.Paused, engine.State.Status);
    }

    [Fact]
    public void HardDrop_ClearsRowsScoresAndAdvancesLevelData()
    {
        var board = new Board(width: 12, height: 8);
        for (var x = 0; x < board.Width; x++)
            if (x is not (4 or 5)) board.SetCell(x, 7, TetrominoType.J);
        var state = new GameState(board, new Piece(TetrominoType.O, 4, 6), new Piece(TetrominoType.T, 4, 0));
        var engine = new GameEngine(new SequencePieceSource(), state);

        engine.HardDrop();

        Assert.Equal(100, state.Score);
        Assert.Equal(100, state.Score);
        Assert.Equal(1, state.TotalLines);
        Assert.Equal(1, state.Level);
        Assert.Equal(TetrominoType.O, state.Board.GetCell(4, 7));
        Assert.Equal(GameStatus.Running, state.Status);
    }

    [Fact]
    public void HardDrop_ClearsMultipleRowsAndAwardsOneHundredPerRow()
    {
        var board = new Board(width: 12, height: 8);
        for (var y = 6; y <= 7; y++)
            for (var x = 0; x < board.Width; x++)
                if (x is not (4 or 5)) board.SetCell(x, y, TetrominoType.L);
        var state = new GameState(board, new Piece(TetrominoType.O, 4, 0), new Piece(TetrominoType.T, 4, 0));
        var engine = new GameEngine(new SequencePieceSource(), state);

        engine.HardDrop();

        Assert.Equal(200, state.Score);
        Assert.Equal(2, state.TotalLines);
        Assert.Equal(1, state.Level);
    }

    [Fact]
    public void DropInterval_DecreasesAtLevelBoundaryAndHasMinimum()
    {
        var state = new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.I, 4, 0), totalLines: 10);
        var engine = new GameEngine(new SequencePieceSource(), state);

        Assert.Equal(730, engine.DropIntervalMilliseconds);
        var fastState = new GameState(new Board(), state.CurrentPiece, state.NextPiece, totalLines: 200);
        Assert.Equal(GameEngine.MinimumDropIntervalMilliseconds, new GameEngine(new SequencePieceSource(), fastState).DropIntervalMilliseconds);
    }

    [Fact]
    public void Constructor_SetsGameOverWhenSpawnIsBlocked()
    {
        var board = new Board();
        var piece = new Piece(TetrominoType.O, 4, 0);
        board.SetCell(4, 0, TetrominoType.I);
        var engine = new GameEngine(new SequencePieceSource(), new GameState(board, piece, piece));

        Assert.Equal(GameStatus.GameOver, engine.State.Status);
    }

    [Fact]
    public void GameStateClone_CopiesBoardAndGameValuesIndependently()
    {
        var board = new Board();
        board.SetCell(1, 1, TetrominoType.S);
        var original = new GameState(board, new Piece(TetrominoType.I, 3, 2), new Piece(TetrominoType.O, 4, 0), 300, 10);
        var clone = original.Clone();
        clone.Board.SetCell(1, 1, null);
        clone.Board.SetCell(2, 2, TetrominoType.J);

        Assert.Equal(300, clone.Score);
        Assert.Equal(2, clone.Level);
        Assert.Equal(TetrominoType.S, original.Board.GetCell(1, 1));
        Assert.Null(original.Board.GetCell(2, 2));
    }

    [Fact]
    public void ReceiveGarbageRows_TopsOutWhenItPushesBlocksPastCeiling()
    {
        var board = new Board(width: 6, height: 6);
        board.SetCell(2, 0, TetrominoType.J);
        var state = new GameState(board, new Piece(TetrominoType.O, 2, 2), new Piece(TetrominoType.T, 2, 0));
        var engine = new GameEngine(new SequencePieceSource(), state);

        engine.ReceiveGarbageRows(1, holeX: 0);

        Assert.Equal(GameStatus.GameOver, state.Status);
    }

    [Fact]
    public void Engine_UsesInjectedScoringRule()
    {
        var board = new Board(width: 12, height: 8);
        for (var x = 0; x < board.Width; x++) if (x is not (4 or 5)) board.SetCell(x, 7, TetrominoType.J);
        var state = new GameState(board, new Piece(TetrominoType.O, 4, 6), new Piece(TetrominoType.T, 4, 0));
        var engine = new GameEngine(new SequencePieceSource(), state, new DoubleScoreRule());

        engine.HardDrop();

        Assert.Equal(200, state.Score);
    }

    private sealed class DoubleScoreRule : IScoringRule
    {
        public int CalculateScore(int clearedRows, int level) => 200 * clearedRows;
    }

    private sealed class SequencePieceSource : IGamePieceSource
    {
        private int _index;
        private readonly TetrominoType[] _types = [TetrominoType.I, TetrominoType.S, TetrominoType.Z];
        public TetrominoType NextType() => _types[_index++ % _types.Length];
    }
}
