using Tetris.ConsoleApp;
using Tetris.Core.Domain;
using Tetris.Core.Game;
using Tetris.Core.Persistence;

namespace Tetris.Tests;

public sealed class ConsoleModuleTests
{
    [Theory]
    [InlineData(ConsoleKey.D1, GameCommand.StartSinglePlayer)]
    [InlineData(ConsoleKey.D2, GameCommand.StartTwoPlayer)]
    [InlineData(ConsoleKey.D3, GameCommand.StartAiDemo)]
    [InlineData(ConsoleKey.D4, GameCommand.ShowLeaderboard)]
    [InlineData(ConsoleKey.A, GameCommand.PlayerOneLeft)]
    [InlineData(ConsoleKey.D, GameCommand.PlayerOneRight)]
    [InlineData(ConsoleKey.W, GameCommand.PlayerOneRotate)]
    [InlineData(ConsoleKey.S, GameCommand.PlayerOneSoftDrop)]
    [InlineData(ConsoleKey.LeftArrow, GameCommand.PlayerTwoLeft)]
    [InlineData(ConsoleKey.RightArrow, GameCommand.PlayerTwoRight)]
    [InlineData(ConsoleKey.UpArrow, GameCommand.PlayerTwoRotate)]
    [InlineData(ConsoleKey.DownArrow, GameCommand.PlayerTwoSoftDrop)]
    [InlineData(ConsoleKey.Enter, GameCommand.PlayerTwoHardDrop)]
    [InlineData(ConsoleKey.Spacebar, GameCommand.PlayerOneHardDrop)]
    [InlineData(ConsoleKey.P, GameCommand.TogglePause)]
    [InlineData(ConsoleKey.M, GameCommand.ToggleAi)]
    [InlineData(ConsoleKey.R, GameCommand.Restart)]
    [InlineData(ConsoleKey.Escape, GameCommand.MainMenu)]
    [InlineData(ConsoleKey.Q, GameCommand.Quit)]
    [InlineData(ConsoleKey.F5, GameCommand.SaveReplay)]
    [InlineData(ConsoleKey.F6, GameCommand.PlayReplay)]
    [InlineData(ConsoleKey.F8, GameCommand.ToggleReplayPause)]
    [InlineData(ConsoleKey.F9, GameCommand.ToggleAiDemoSpeed)]
    [InlineData(ConsoleKey.F1, GameCommand.None)]
    public void InputMapper_MapsKeysToCommands(ConsoleKey key, GameCommand expected) =>
        Assert.Equal(expected, GameInputMapper.Map(key));

    [Fact]
    public void Controller_HandlesPauseAiAndQuitCommands()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-controller-{Guid.NewGuid():N}");
        try
        {
            var engine = new GameEngine(new TestPieceSource());
            var controller = new GameController(engine, new HighScoreStore(Path.Combine(directory, "highscore.xml")));
            Assert.Equal(GameScreen.MainMenu, controller.Screen);
            controller.Handle(GameCommand.StartSinglePlayer);
            Assert.Equal(GameScreen.Playing, controller.Screen);
            controller.Handle(GameCommand.TogglePause);
            Assert.Equal(GameScreen.Paused, controller.Screen);
            Assert.Equal(GameStatus.Paused, engine.State.Status);
            controller.Handle(GameCommand.TogglePause);
            controller.Handle(GameCommand.ToggleAi);
            Assert.True(controller.AiEnabled);
            controller.Handle(GameCommand.Quit);
            Assert.True(controller.IsQuitRequested);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Controller_CanExecuteAnAiTurn()
    {
        var engine = new GameEngine(new TestPieceSource());
        var path = Path.Combine(Path.GetTempPath(), $"tetris-controller-{Guid.NewGuid():N}", "highscore.xml");
        var controller = new GameController(engine, new HighScoreStore(path));
        controller.Handle(GameCommand.StartAiDemo);

        Assert.True(controller.PerformAiTurn());
        Assert.Equal(GameStatus.Running, engine.State.Status);
        Assert.Equal(0, Enumerable.Range(0, engine.State.Board.Width).Count(x => engine.State.Board.GetCell(x, 15).HasValue));
        Assert.InRange(engine.State.CurrentPiece.Y, 0, 1);
    }

    [Fact]
    public void AiDemo_RejectsHumanControlsAndKeepsAiEnabled()
    {
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartAiDemo);
        var x = controller.Engine.State.CurrentPiece.X;

        controller.Handle(GameCommand.PlayerOneLeft);
        controller.Handle(GameCommand.PlayerTwoRight);
        controller.Handle(GameCommand.PlayerOneHardDrop);
        controller.Handle(GameCommand.ToggleAi);

        Assert.Equal(x, controller.Engine.State.CurrentPiece.X);
        Assert.Equal(GameStatus.Running, controller.Engine.State.Status);
        Assert.True(controller.AiEnabled);
    }

    [Fact]
    public void AiDemo_TogglesBetweenNormalAndFastObservationSpeed()
    {
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartAiDemo);

        Assert.Equal(1, controller.AiDemoSpeedMultiplier);
        controller.Handle(GameCommand.ToggleAiDemoSpeed);
        Assert.Equal(2, controller.AiDemoSpeedMultiplier);
        controller.Handle(GameCommand.ToggleAiDemoSpeed);
        Assert.Equal(1, controller.AiDemoSpeedMultiplier);
    }

    [Fact]
    public void Formatter_ExplainsAiDemoControlsAndCurrentSpeed()
    {
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartAiDemo);

        var normal = new GameViewFormatter().Format(controller, 100, 30);
        Assert.Contains("AI 自动操作中", normal);
        Assert.Contains("普通", normal);
        Assert.DoesNotContain("M 切换 AI", normal);

        controller.Handle(GameCommand.ToggleAiDemoSpeed);
        var fast = new GameViewFormatter().Format(controller, 100, 30);
        Assert.Contains("2 倍速", fast);
        Assert.Contains("F9", fast);
    }

    [Fact]
    public void TwoPlayer_EnginesAndBoardsAreIndependentAcrossInputsAndGarbage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-isolation-{Guid.NewGuid():N}");
        try
        {
            var controller = new GameController(new HighScoreStore(Path.Combine(directory, "score.xml")),
                seed => new GameEngine(new RandomPieceSource(seed)), new ReplayStore(Path.Combine(directory, "replays")));
            controller.Handle(GameCommand.StartTwoPlayer);
            var first = controller.Engine;
            var second = controller.SecondEngine!;
            Assert.NotSame(first, second);
            Assert.NotSame(first.State, second.State);
            Assert.NotSame(first.State.Board, second.State.Board);

            first.State.Board.SetCell(0, 15, TetrominoType.I);
            Assert.Null(second.State.Board.GetCell(0, 15));
            second.State.Board.SetCell(11, 15, TetrominoType.Z);
            Assert.Null(first.State.Board.GetCell(11, 15));

            var firstX = first.State.CurrentPiece.X;
            var secondX = second.State.CurrentPiece.X;
            controller.Handle(GameCommand.PlayerOneLeft);
            Assert.Equal(firstX - 1, first.State.CurrentPiece.X);
            Assert.Equal(secondX, second.State.CurrentPiece.X);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void AiTurn_OnlySteersPieceAndLeavesGravityToNormalGameTick()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tetris-ai-steer-{Guid.NewGuid():N}", "score.xml");
        var engine = new GameEngine(new TestPieceSource());
        var controller = new GameController(engine, new HighScoreStore(path));
        controller.Handle(GameCommand.StartAiDemo);
        var startingY = engine.State.CurrentPiece.Y;
        var boardBefore = engine.State.Board.Clone();

        Assert.True(controller.PerformAiTurn());

        Assert.Equal(startingY, engine.State.CurrentPiece.Y);
        Assert.Equal(0, Enumerable.Range(0, 16).Sum(y => Enumerable.Range(0, 12).Count(x => boardBefore.GetCell(x, y).HasValue)));
        Assert.Equal(0, Enumerable.Range(0, 16).Sum(y => Enumerable.Range(0, 12).Count(x => engine.State.Board.GetCell(x, y).HasValue)));
        controller.AdvanceTime();
        Assert.Equal(startingY + 1, engine.State.CurrentPiece.Y);
    }

    [Fact]
    public void TwoPlayerAi_ControlsOnlyPlayerTwoAndBothBoardsReceiveGravity()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-two-ai-{Guid.NewGuid():N}");
        try
        {
            var controller = new GameController(new HighScoreStore(Path.Combine(directory, "score.xml")),
                seed => new GameEngine(new RandomPieceSource(seed)), new ReplayStore(Path.Combine(directory, "replays")));
            controller.Handle(GameCommand.StartTwoPlayer);
            var first = controller.Engine;
            var second = controller.SecondEngine!;
            var firstY = first.State.CurrentPiece.Y;
            var secondY = second.State.CurrentPiece.Y;
            var firstX = first.State.CurrentPiece.X;
            controller.Handle(GameCommand.ToggleAi);

            Assert.True(controller.PerformAiTurn());
            Assert.Equal(firstY, first.State.CurrentPiece.Y);
            Assert.Equal(secondY, second.State.CurrentPiece.Y);
            var secondX = second.State.CurrentPiece.X;
            controller.Handle(GameCommand.PlayerOneRight);
            Assert.Equal(firstX + 1, first.State.CurrentPiece.X);
            controller.Handle(GameCommand.PlayerTwoRight);
            Assert.Equal(secondX, second.State.CurrentPiece.X);

            controller.AdvanceTime();
            Assert.Equal(firstY + 1, first.State.CurrentPiece.Y);
            Assert.Equal(secondY + 1, second.State.CurrentPiece.Y);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Controller_ReportsWhenHighScoreCannotBeSaved()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-controller-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var engine = new GameEngine(new TestPieceSource(),
                new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.I, 4, 0), score: 500));
            var controller = new GameController(engine, new HighScoreStore(directory));
            controller.Handle(GameCommand.StartSinglePlayer);
            Assert.True(controller.HighScoreSaveFailed);
            Assert.Equal(500, controller.HighScore);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Formatter_ShowsDecimalScoreLevelLinesAndTerminalSizeMessage()
    {
        var state = new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.O, 4, 0), 250, 11);
        var formatter = new GameViewFormatter();
        var frame = formatter.Format(state, 512, aiEnabled: true, highScoreSaveFailed: false, 80, 30);

        Assert.Contains("分数 250", frame);
        Assert.Contains("最高分 512", frame);
        Assert.DoesNotContain("0x", frame);
        Assert.Contains("等级 2", frame);
        Assert.Contains("已消行 11", frame);
        Assert.Contains("至少 80 列 × 23 行", formatter.Format(state, 0, false, false, 40, 20));
    }

    [Fact]
    public void Controller_StartsTwoPlayerModeAndMovesBoardsIndependently()
    {
        var engine = new GameEngine(new TestPieceSource());
        var controller = new GameController(engine, new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartTwoPlayer);
        var firstStart = controller.Engine.State.CurrentPiece.X;
        var secondStart = controller.SecondEngine!.State.CurrentPiece.X;

        controller.Handle(GameCommand.PlayerOneLeft);
        controller.Handle(GameCommand.PlayerTwoRight);

        Assert.Equal(GameMode.TwoPlayer, controller.Mode);
        Assert.NotNull(controller.SecondEngine);
        Assert.Equal(firstStart - 1, controller.Engine.State.CurrentPiece.X);
        Assert.Equal(secondStart + 1, controller.SecondEngine.State.CurrentPiece.X);
    }

    [Fact]
    public void TwoPlayer_MultiLineClearAddsGarbageRowsToOpponent()
    {
        var board = new Board(width: 12, height: 8);
        for (var y = 6; y <= 7; y++)
            for (var x = 0; x < board.Width; x++)
                if (x is not (4 or 5)) board.SetCell(x, y, TetrominoType.L);
        var state = new GameState(board, new Piece(TetrominoType.O, 4, 0), new Piece(TetrominoType.T, 4, 0));
        var engine = new GameEngine(new TestPieceSource(), state);
        var controller = new GameController(engine, new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartTwoPlayer);

        controller.Handle(GameCommand.PlayerOneHardDrop);

        Assert.Equal(200, engine.State.Score);
        Assert.NotNull(controller.SecondEngine);
        Assert.Equal(11, Enumerable.Range(0, 12).Count(x => controller.SecondEngine.State.Board.GetCell(x, 14).HasValue));
        Assert.Equal(11, Enumerable.Range(0, 12).Count(x => controller.SecondEngine.State.Board.GetCell(x, 15).HasValue));
        controller.Handle(GameCommand.PlayerOneRight);
        Assert.Equal(0, Enumerable.Range(0, 12).Count(x => controller.SecondEngine.State.Board.GetCell(x, 13).HasValue));
    }

    [Fact]
    public void Controller_ResumesPauseAndReturnsToMenu()
    {
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartSinglePlayer);
        controller.Handle(GameCommand.TogglePause);
        controller.AdvanceTime();
        Assert.Equal(GameScreen.Paused, controller.Screen);
        controller.Handle(GameCommand.TogglePause);
        Assert.Equal(GameScreen.Playing, controller.Screen);
        controller.Handle(GameCommand.MainMenu);
        Assert.Equal(GameScreen.MainMenu, controller.Screen);
    }

    [Fact]
    public void Formatter_ShowsStartMenuAndTwoPlayerBoardsSideBySide()
    {
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        var formatter = new GameViewFormatter();
        Assert.Contains("2  双人本地对战", formatter.Format(controller, 120, 30));
        Assert.Contains("4  排行榜", formatter.Format(controller, 120, 30));

        controller.Handle(GameCommand.StartTwoPlayer);
        var frame = formatter.Format(controller, 120, 30);
        Assert.Contains("玩家一 vs 玩家二", frame);
        Assert.Contains("玩家一：A/D", frame);
        Assert.Contains("玩家二：←/→", frame);
        Assert.Contains("垃圾行", frame);
        Assert.Contains("至少 110 列", formatter.Format(controller, 90, 30));
    }

    [Fact]
    public void Renderer_RendersTheStartMenuFromController()
    {
        var terminal = new FakeTerminalSurface(100, 30);
        var renderer = new ConsoleGameRenderer(terminal);
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));

        renderer.Render(controller);

        Assert.Contains("选择游戏模式", terminal.Text);
        Assert.Contains("3  AI 观战演示", terminal.Text);
    }

    [Fact]
    public void Renderer_ClearsOldGameFrameWhenReturningToMenu()
    {
        var terminal = new FakeTerminalSurface(120, 30);
        var renderer = new ConsoleGameRenderer(terminal);
        var controller = new GameController(new GameEngine(new TestPieceSource()),
            new HighScoreStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "score.xml")));
        controller.Handle(GameCommand.StartSinglePlayer);
        renderer.Render(controller);
        terminal.WasCleared = false;
        controller.Handle(GameCommand.MainMenu);

        renderer.Render(controller);

        Assert.True(terminal.WasCleared);
        Assert.Contains("选择游戏模式", terminal.Text);
    }

    [Fact]
    public void Controller_SavesCompletedResultAndShowsLeaderboard()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-controller-scores-{Guid.NewGuid():N}");
        try
        {
            var history = new SqliteScoreHistoryStore(Path.Combine(directory, "scores.db"));
            var engine = new GameEngine(new TestPieceSource(),
                new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.I, 4, 0), score: 700, totalLines: 7, status: GameStatus.GameOver));
            var controller = new GameController(engine, new HighScoreStore(Path.Combine(directory, "highscore.xml")), history);
            controller.Handle(GameCommand.StartSinglePlayer);
            controller.AdvanceTime();

            Assert.Equal(GameScreen.GameOver, controller.Screen);
            controller.Handle(GameCommand.MainMenu);
            controller.Handle(GameCommand.ShowLeaderboard);

            Assert.Equal(GameScreen.Leaderboard, controller.Screen);
            var entry = Assert.Single(controller.Leaderboard);
            Assert.Equal(700, entry.Score);
            Assert.Equal("单人游戏", entry.GameMode);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void ReplayStore_RoundTripsAndPlayerHonorsPauseAndSpeed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-replay-{Guid.NewGuid():N}");
        try
        {
            var store = new ReplayStore(directory);
            var document = new ReplayDocument(1, 12345, GameMode.SinglePlayer,
            [
                new ReplayEvent(100, ReplayEventKind.Command, GameCommand.PlayerOneLeft),
                new ReplayEvent(300, ReplayEventKind.Tick)
            ]);
            Assert.True(store.TrySave(document, out _));
            var loaded = store.LoadLatest();
            Assert.NotNull(loaded);
            Assert.Equal(12345, loaded.Seed);
            var player = new ReplayPlayer(loaded);
            player.TogglePause();
            Assert.Empty(player.Advance(TimeSpan.FromSeconds(1)));
            player.TogglePause();
            player.SetSpeed(2);
            Assert.Equal(2, player.Advance(TimeSpan.FromMilliseconds(150)).Count);

            File.WriteAllText(Path.Combine(directory, "replay-corrupt.json"), "{ broken json");
            File.SetLastWriteTimeUtc(Path.Combine(directory, "replay-corrupt.json"), DateTime.UtcNow.AddMinutes(1));
            Assert.NotNull(store.LoadLatest());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Controller_RecordsAndReplaysCommandsFromDeterministicSeed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-replay-controller-{Guid.NewGuid():N}");
        try
        {
            var store = new ReplayStore(directory);
            var controller = new GameController(
                new HighScoreStore(Path.Combine(directory, "highscore.xml")),
                seed => new GameEngine(new RandomPieceSource(seed)), store);
            controller.Handle(GameCommand.StartSinglePlayer);
            var startType = controller.Engine.State.CurrentPiece.Type;
            controller.Handle(GameCommand.PlayerOneLeft);
            controller.AdvanceTime();
            var expectedX = controller.Engine.State.CurrentPiece.X;
            var expectedY = controller.Engine.State.CurrentPiece.Y;
            controller.SaveReplay();

            Assert.True(controller.StartLatestReplay());
            controller.AdvanceReplay(TimeSpan.FromSeconds(1));

            Assert.Equal(startType, controller.Engine.State.CurrentPiece.Type);
            Assert.Equal(expectedX, controller.Engine.State.CurrentPiece.X);
            Assert.Equal(expectedY, controller.Engine.State.CurrentPiece.Y);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void InputSource_ReturnsKeyOnlyWhenAvailableAndHandlesUnavailableConsole()
    {
        var source = new ConsoleInputSource(new FakeKeyReader(ConsoleKey.LeftArrow, available: true));
        Assert.True(source.TryRead(out var key));
        Assert.Equal(ConsoleKey.LeftArrow, key);

        var emptySource = new ConsoleInputSource(new FakeKeyReader(ConsoleKey.None, available: false));
        Assert.False(emptySource.TryRead(out _));

        var failingSource = new ConsoleInputSource(new FailingKeyReader());
        Assert.False(failingSource.TryRead(out _));
    }

    [Fact]
    public void Renderer_WritesFormattedFrameAtTopLeft()
    {
        var terminal = new FakeTerminalSurface(100, 30);
        var renderer = new ConsoleGameRenderer(terminal);
        var state = new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.O, 4, 0));

        renderer.Render(state, 0, aiEnabled: false, highScoreSaveFailed: false);

        Assert.Equal((0, 0), terminal.CursorPosition);
        Assert.Contains("TETRIS 12 × 16", terminal.Text);
        Assert.False(terminal.WasCleared);
    }

    [Fact]
    public void Renderer_ClearsAndPromptsWhenWindowIsTooSmall()
    {
        var terminal = new FakeTerminalSurface(60, 20);
        var renderer = new ConsoleGameRenderer(terminal);
        var state = new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.O, 4, 0));

        renderer.Render(state, 0, aiEnabled: false, highScoreSaveFailed: false);

        Assert.True(terminal.WasCleared);
        Assert.Contains("至少 80 列 × 23 行", terminal.Text);
    }

    [Fact]
    public void Renderer_SwallowsTerminalWriteErrors()
    {
        var renderer = new ConsoleGameRenderer(new FailingTerminalSurface());
        var state = new GameState(new Board(), new Piece(TetrominoType.T, 4, 0), new Piece(TetrominoType.O, 4, 0));

        var exception = Record.Exception(() => renderer.Render(state, 0, false, false));
        Assert.Null(exception);
    }

    private sealed class TestPieceSource : IGamePieceSource
    {
        public TetrominoType NextType() => TetrominoType.T;
    }

    private sealed class FakeKeyReader(ConsoleKey key, bool available) : IConsoleKeyReader
    {
        public bool IsKeyAvailable => available;
        public ConsoleKey ReadKey() => key;
    }

    private sealed class FailingKeyReader : IConsoleKeyReader
    {
        public bool IsKeyAvailable => throw new InvalidOperationException("No interactive console.");
        public ConsoleKey ReadKey() => throw new InvalidOperationException("No key.");
    }

    private sealed class FakeTerminalSurface(int width, int height) : ITerminalSurface
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public bool WasCleared { get; set; }
        public (int Left, int Top) CursorPosition { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public void Clear() => WasCleared = true;
        public void SetCursorPosition(int left, int top) => CursorPosition = (left, top);
        public void Write(string text) => Text = text;
    }

    private sealed class FailingTerminalSurface : ITerminalSurface
    {
        public int Width => 100;
        public int Height => 30;
        public void Clear() { }
        public void SetCursorPosition(int left, int top) => throw new IOException("Terminal closed.");
        public void Write(string text) => throw new IOException("Terminal closed.");
    }
}
