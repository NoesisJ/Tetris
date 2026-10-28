using Tetris.ConsoleApp;
using Tetris.Core.Game;
using Tetris.Core.Persistence;

namespace Tetris.Tests;

public sealed class ReplayLibraryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"tetris-replay-library-{Guid.NewGuid():N}");
    private ReplayStore Store => new(Path.Combine(_directory, "replays"));

    [Theory]
    [InlineData(ConsoleKey.D5, GameCommand.PlayReplay)]
    [InlineData(ConsoleKey.NumPad5, GameCommand.PlayReplay)]
    [InlineData(ConsoleKey.PageUp, GameCommand.ReplayPageUp)]
    [InlineData(ConsoleKey.PageDown, GameCommand.ReplayPageDown)]
    public void InputMapper_MapsLibraryShortcuts(ConsoleKey key, GameCommand command) =>
        Assert.Equal(command, GameInputMapper.Map(key));

    [Fact]
    public void Store_ListsEveryValidReplayBySaveTimeAndLoadsRequestedFile()
    {
        var older = Save(111, 0, GameMode.TwoPlayer, GameCommand.PlayerOneLeft);
        var newer = Save(222, 1, GameMode.AiDemo, GameCommand.PlayerOneRight);
        File.WriteAllText(Path.Combine(_directory, "replays", "replay-broken.json"), "{ broken json");

        var entries = Store.ListReplays(out var warning);

        Assert.Equal(2, entries.Count);
        Assert.Equal(newer, entries[0].Path);
        Assert.Equal(older, entries[1].Path);
        Assert.Equal(GameMode.TwoPlayer, entries[1].Mode);
        Assert.Equal(1000, entries[1].DurationMilliseconds);
        Assert.Equal(111, Store.Load(entries[1].Path)!.Seed);
        Assert.Equal(222, Store.LoadLatest()!.Seed);
        Assert.Contains("跳过 1 个", warning);
    }

    [Fact]
    public void Store_HandlesMissingDirectoryAndEmptyRecording()
    {
        Assert.Empty(Store.ListReplays(out var error));
        Assert.Null(error);
        Assert.True(Store.TrySave(new ReplayDocument(1, 42, GameMode.SinglePlayer, []), out _));
        Assert.Equal(0, Assert.Single(Store.ListReplays(out _)).DurationMilliseconds);
    }

    [Fact]
    public void Store_SkipsLockedFileWhileOtherReplaysRemainAvailable()
    {
        var lockedPath = Save(111, 0);
        var readablePath = Save(222, 1);
        using var fileLock = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var entries = Store.ListReplays(out var warning);
        Assert.Equal(readablePath, Assert.Single(entries).Path);
        Assert.Null(Store.Load(lockedPath));
        Assert.NotNull(warning);
    }

    [Fact]
    public void Store_ReportsUnreadableDirectoryInsteadOfThrowing()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "not-a-directory");
        File.WriteAllText(file, "blocked");
        Assert.Empty(new ReplayStore(file).ListReplays(out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"Version\":1,\"Seed\":42,\"Mode\":0,\"Events\":null}")]
    [InlineData("{\"Version\":99,\"Seed\":42,\"Mode\":0,\"Events\":[]}")]
    [InlineData("{\"Version\":1,\"Seed\":42,\"Mode\":0,\"Events\":[null]}")]
    [InlineData("{\"Version\":1,\"Seed\":42,\"Mode\":0,\"Events\":[{\"TimeMilliseconds\":-1}]}")]
    public void Store_SkipsSemanticallyInvalidFiles(string json)
    {
        Directory.CreateDirectory(Path.Combine(_directory, "replays"));
        var file = Path.Combine(_directory, "replays", "replay-invalid.json");
        File.WriteAllText(file, json);
        Assert.Null(Store.Load(file));
        Assert.Empty(Store.ListReplays(out var warning));
        Assert.NotNull(warning);
    }

    [Fact]
    public void Controller_SelectsOlderReplayAndReconstructsItsOwnSeedAndCommands()
    {
        Save(111, 0, command: GameCommand.PlayerOneLeft);
        Save(222, 1, command: GameCommand.PlayerOneRight);
        var controller = Controller();
        controller.Handle(GameCommand.PlayReplay);
        Assert.Equal(GameScreen.ReplayLibrary, controller.Screen);
        Assert.False(controller.IsReplaying);
        controller.Handle(GameInputMapper.Map(ConsoleKey.DownArrow));
        controller.Handle(GameInputMapper.Map(ConsoleKey.Enter));

        var expected = new GameEngine(new RandomPieceSource(111));
        expected.MoveHorizontal(-1);
        controller.AdvanceReplay(TimeSpan.FromMilliseconds(1000));

        Assert.True(controller.IsReplaying);
        Assert.Equal(GameScreen.Playing, controller.Screen);
        Assert.Equal(expected.State.CurrentPiece, controller.Engine.State.CurrentPiece);
        Assert.Contains("回放已结束", controller.ReplayMessage);
    }

    [Theory]
    [InlineData(GameMode.SinglePlayer)]
    [InlineData(GameMode.TwoPlayer)]
    [InlineData(GameMode.AiDemo)]
    public void Controller_BrowsesFromEveryGameModeAndStopsLiveUpdates(GameMode mode)
    {
        Save(111, 0);
        var controller = Controller();
        controller.Handle(mode switch
        {
            GameMode.TwoPlayer => GameCommand.StartTwoPlayer,
            GameMode.AiDemo => GameCommand.StartAiDemo,
            _ => GameCommand.StartSinglePlayer
        });
        controller.Handle(GameCommand.PlayReplay);
        var piece = controller.Engine.State.CurrentPiece;
        controller.AdvanceTime();
        controller.AdvanceReplay(TimeSpan.FromSeconds(10));
        Assert.False(controller.PerformAiTurn());
        Assert.Equal(piece, controller.Engine.State.CurrentPiece);
        Assert.Equal(GameScreen.ReplayLibrary, controller.Screen);

        controller.Handle(GameCommand.MainMenu);
        Assert.Equal(GameScreen.MainMenu, controller.Screen);
        controller.Handle(GameCommand.StartSinglePlayer);
        Assert.Equal(GameScreen.Playing, controller.Screen);
        Assert.False(controller.IsReplaying);
        Assert.Null(controller.ReplayMessage);
    }

    [Fact]
    public void Controller_NavigatesAcrossPagesAndClampsAtBothEnds()
    {
        for (var i = 0; i < 10; i++) Save(i, i);
        var controller = Controller();
        controller.Handle(GameCommand.PlayReplay);
        controller.Handle(GameCommand.PlayerOneRotate);
        Assert.Equal(0, controller.SelectedReplayIndex);
        controller.Handle(GameCommand.ReplayPageDown);
        Assert.Equal(8, controller.SelectedReplayIndex);
        var frame = new GameViewFormatter().Format(controller, 80, 23);
        Assert.Contains("第 2/2 页，共 10 份录像", frame);
        Assert.Contains(Path.GetFileName(controller.Replays[8].Path), frame);
        Assert.DoesNotContain(Path.GetFileName(controller.Replays[0].Path), frame);
        Assert.InRange(frame.Split(Environment.NewLine).Length, 1, 22);
        for (var i = 0; i < 20; i++) controller.Handle(GameCommand.PlayerOneSoftDrop);
        Assert.Equal(9, controller.SelectedReplayIndex);
        controller.Handle(GameCommand.ReplayPageUp);
        Assert.Equal(1, controller.SelectedReplayIndex);
        controller.Handle(GameCommand.PlayerOneRotate);
        Assert.Equal(0, controller.SelectedReplayIndex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Controller_HandlesReplayDeletedOrCorruptedAfterListing(bool corrupt)
    {
        var path = Save(111, 0);
        var controller = Controller();
        controller.Handle(GameCommand.PlayReplay);
        if (corrupt) File.WriteAllText(path, "{ broken json");
        else File.Delete(path);
        controller.Handle(GameCommand.PlayerTwoHardDrop);
        Assert.Equal(GameScreen.ReplayLibrary, controller.Screen);
        Assert.False(controller.IsReplaying);
        Assert.Contains("删除、损坏或无法读取", controller.ReplayLibraryError);
        controller.Handle(GameCommand.PlayReplay);
        Assert.Empty(controller.Replays);
        controller.Handle(GameCommand.PlayerOneSoftDrop);
        controller.Handle(GameCommand.PlayerTwoHardDrop);
        Assert.Equal(0, controller.SelectedReplayIndex);
        Assert.Equal(GameScreen.ReplayLibrary, controller.Screen);
        Assert.Contains("暂无可播放录像", new GameViewFormatter().Format(controller, 80, 23));
    }

    [Fact]
    public void Controller_LoadsAnotherReplayAfterReturningToLibrary()
    {
        Save(111, 0, command: GameCommand.PlayerOneLeft);
        Save(222, 1, command: GameCommand.PlayerOneRight);
        var controller = Controller();
        controller.Handle(GameCommand.PlayReplay);
        controller.Handle(GameCommand.PlayerOneHardDrop);
        controller.AdvanceReplay(TimeSpan.FromMilliseconds(100));
        controller.Handle(GameCommand.PlayReplay);
        controller.Handle(GameCommand.PlayerOneSoftDrop);
        controller.Handle(GameCommand.PlayerOneHardDrop);
        controller.AdvanceReplay(TimeSpan.FromMilliseconds(1000));

        var expected = new GameEngine(new RandomPieceSource(111));
        expected.MoveHorizontal(-1);
        Assert.Equal(expected.State.CurrentPiece, controller.Engine.State.CurrentPiece);
    }

    [Fact]
    public void Playback_IgnoresLiveCommandsHonorsPauseAndSpeedAndFreezesAtEnd()
    {
        Save(111, 0, command: GameCommand.PlayerOneLeft);
        var controller = Controller();
        controller.Handle(GameCommand.PlayReplay);
        controller.Handle(GameCommand.PlayerOneHardDrop);
        var start = controller.Engine.State.CurrentPiece;
        controller.Handle(GameCommand.PlayerOneRight);
        controller.Handle(GameCommand.TogglePause);
        controller.Handle(GameCommand.Restart);
        controller.Handle(GameCommand.ToggleAi);
        controller.AdvanceTime();
        Assert.Equal(start, controller.Engine.State.CurrentPiece);
        controller.Handle(GameCommand.ToggleReplayPause);
        controller.AdvanceReplay(TimeSpan.FromSeconds(2));
        Assert.Equal(start, controller.Engine.State.CurrentPiece);
        controller.Handle(GameCommand.ToggleReplayPause);
        controller.Handle(GameCommand.ReplayFaster);
        controller.AdvanceReplay(TimeSpan.FromMilliseconds(500));
        Assert.Equal(start with { X = start.X - 1 }, controller.Engine.State.CurrentPiece);
        var final = controller.Engine.State.CurrentPiece;
        controller.AdvanceTime();
        controller.AdvanceReplay(TimeSpan.FromSeconds(60));
        controller.Handle(GameCommand.PlayerOneHardDrop);
        Assert.Equal(final, controller.Engine.State.CurrentPiece);
        controller.Handle(GameCommand.MainMenu);
        Assert.False(controller.IsReplaying);
    }

    [Fact]
    public void Library_UsesSinglePlayerWidthEvenAfterTwoPlayerGame()
    {
        Save(111, 0, GameMode.TwoPlayer);
        var controller = Controller();
        controller.Handle(GameCommand.StartTwoPlayer);
        controller.Handle(GameCommand.PlayReplay);
        var frame = new GameViewFormatter().Format(controller, 80, 23);
        Assert.Contains("录像列表", frame);
        Assert.Contains("双人对战", frame);
        Assert.Contains("1 秒", frame);
        Assert.DoesNotContain("至少 110", frame);
        Assert.Equal(80, GameViewFormatter.RequiredWidth(controller));
    }

    [Fact]
    public void Renderer_ClearsPreviousGameAndLibraryWhenSwitchingScreens()
    {
        Save(111, 0);
        var controller = Controller();
        var terminal = new RecordingTerminal();
        var renderer = new ConsoleGameRenderer(terminal);
        controller.Handle(GameCommand.StartTwoPlayer);
        renderer.Render(controller);
        var clearCount = terminal.ClearCount;

        controller.Handle(GameCommand.PlayReplay);
        renderer.Render(controller);
        Assert.Equal(clearCount + 1, terminal.ClearCount);
        Assert.Contains("录像列表", terminal.Frame);
        Assert.DoesNotContain("至少 110", terminal.Frame);

        controller.Handle(GameCommand.MainMenu);
        renderer.Render(controller);
        Assert.Equal(clearCount + 2, terminal.ClearCount);
        Assert.Contains("5  录像列表", terminal.Frame);
    }

    private sealed class RecordingTerminal : ITerminalSurface
    {
        public int Width => 80;
        public int Height => 23;
        public int ClearCount { get; private set; }
        public string Frame { get; private set; } = "";
        public void Clear() => ClearCount++;
        public void SetCursorPosition(int left, int top) { }
        public void Write(string text) => Frame = text;
    }

    private string Save(int seed, int order, GameMode mode = GameMode.SinglePlayer,
        GameCommand command = GameCommand.PlayerOneLeft)
    {
        Assert.True(Store.TrySave(new ReplayDocument(1, seed, mode,
            [new ReplayEvent(1000, ReplayEventKind.Command, command)]), out var path));
        File.SetLastWriteTimeUtc(path!, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(order));
        return path!;
    }

    private GameController Controller() => new(new HighScoreStore(Path.Combine(_directory, "highscore.xml")),
        seed => new GameEngine(new RandomPieceSource(seed)), Store);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
