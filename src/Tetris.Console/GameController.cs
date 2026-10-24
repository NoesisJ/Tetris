using Tetris.Core.AI;
using Tetris.Core.Domain;
using Tetris.Core.Game;
using Tetris.Core.Persistence;

namespace Tetris.ConsoleApp;

public sealed class GameController
{
    private readonly HighScoreStore _highScoreStore;
    private readonly AiPlanner _aiPlanner = new();
    private Random _garbageRandom = new();
    private readonly Func<int, GameEngine> _engineFactory;
    private readonly ReplayStore? _replayStore;
    private GameEngine? _providedInitialEngine;
    private ReplayRecorder? _recorder;
    private ReplayPlayer? _replayPlayer;
    private bool _applyingReplay;
    private bool _replaying;
    private int _recordingSeed;

    public GameController(GameEngine engine, HighScoreStore highScoreStore)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _providedInitialEngine = engine;
        _engineFactory = seed => new GameEngine(new RandomPieceSource(seed));
        _highScoreStore = highScoreStore ?? throw new ArgumentNullException(nameof(highScoreStore));
        HighScore = Math.Max(0, highScoreStore.Load());
    }

    public GameController(HighScoreStore highScoreStore, Func<int, GameEngine> engineFactory, ReplayStore replayStore)
    {
        _highScoreStore = highScoreStore ?? throw new ArgumentNullException(nameof(highScoreStore));
        _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        _replayStore = replayStore ?? throw new ArgumentNullException(nameof(replayStore));
        _recordingSeed = Random.Shared.Next();
        Engine = _engineFactory(_recordingSeed);
        HighScore = Math.Max(0, highScoreStore.Load());
    }

    public int HighScore { get; private set; }
    public bool HighScoreSaveFailed { get; private set; }
    public bool AiEnabled { get; private set; }
    public bool IsQuitRequested { get; private set; }
    public GameScreen Screen { get; private set; } = GameScreen.MainMenu;
    public GameMode Mode { get; private set; } = GameMode.SinglePlayer;
    public GameEngine Engine { get; private set; }
    public GameEngine? SecondEngine { get; private set; }
    public string? GameOverMessage { get; private set; }
    public bool IsReplaying => _replaying;
    public bool ReplayPaused => _replayPlayer?.IsPaused ?? false;
    public double ReplaySpeed => _replayPlayer?.Speed ?? 1;
    public string? ReplayMessage { get; private set; }

    public void Handle(GameCommand command)
    {
        if (command == GameCommand.SaveReplay) { SaveReplay(); return; }
        if (command == GameCommand.PlayReplay) { StartLatestReplay(); return; }
        if (command == GameCommand.ToggleReplayPause)
        {
            if (_replayPlayer is not null)
            {
                _replayPlayer.TogglePause();
                ReplayMessage = _replayPlayer.IsPaused ? "回放已暂停。按 F8 继续，+/- 调速。" : $"正在回放，速度 {_replayPlayer.Speed:0.##}×。F8 暂停，+/- 调速。";
            }
            return;
        }
        if (command is GameCommand.ReplayFaster or GameCommand.ReplaySlower)
        {
            if (_replayPlayer is not null)
            {
                _replayPlayer.SetSpeed(_replayPlayer.Speed * (command == GameCommand.ReplayFaster ? 2 : 0.5));
                ReplayMessage = $"{(_replayPlayer.IsPaused ? "回放已暂停" : "正在回放")}，速度 {_replayPlayer.Speed:0.##}×。F8 暂停，+/- 调速。";
            }
            return;
        }
        if (command == GameCommand.Quit)
        {
            IsQuitRequested = true;
            return;
        }

        if (Screen == GameScreen.MainMenu)
        {
            switch (command)
            {
                case GameCommand.StartSinglePlayer: StartMode(GameMode.SinglePlayer); SaveHighScoreIfNeeded(); break;
                case GameCommand.StartTwoPlayer: StartMode(GameMode.TwoPlayer); SaveHighScoreIfNeeded(); break;
                case GameCommand.StartAiDemo: StartMode(GameMode.AiDemo); SaveHighScoreIfNeeded(); break;
            }
            return;
        }

        if (Screen == GameScreen.GameOver)
        {
            if (command == GameCommand.Restart) { Restart(); Record(ReplayEventKind.Command, command); }
            else if (command == GameCommand.MainMenu)
            {
                Screen = GameScreen.MainMenu;
                _replayPlayer = null;
                _replaying = false;
            }
            return;
        }

        if (command == GameCommand.MainMenu)
        {
            Screen = GameScreen.MainMenu;
            _replayPlayer = null;
            _replaying = false;
            return;
        }

        if (command == GameCommand.TogglePause)
        {
            TogglePause();
            Record(ReplayEventKind.Command, command);
            return;
        }

        if (Screen == GameScreen.Paused) return;

        if (command == GameCommand.ToggleAi)
        {
            AiEnabled = !AiEnabled;
            Record(ReplayEventKind.Command, command);
            return;
        }

        if (command == GameCommand.Restart)
        {
            Restart();
            Record(ReplayEventKind.Command, command);
            return;
        }

        ApplyPlayerCommand(command);
        Record(ReplayEventKind.Command, command);
        UpdateScreenFromGameState();
        SaveHighScoreIfNeeded();
    }

    public void AdvanceTime()
    {
        if (Screen != GameScreen.Playing) return;
        Record(ReplayEventKind.Tick);
        if (!ControlsPlayerOneWithAi) AdvanceEngine(Engine);
        if (Mode == GameMode.TwoPlayer && !ControlsPlayerTwoWithAi && SecondEngine is not null)
            AdvanceEngine(SecondEngine);
        UpdateScreenFromGameState();
        SaveHighScoreIfNeeded();
    }

    public void AdvanceReplay(TimeSpan elapsed)
    {
        if (_recorder is not null && !_replaying) _recorder.Advance(elapsed);
        if (_replayPlayer is null) return;
        foreach (var replayEvent in _replayPlayer.Advance(elapsed))
        {
            _applyingReplay = true;
            try
            {
                switch (replayEvent.Kind)
                {
                    case ReplayEventKind.Command: Handle(replayEvent.Command); break;
                    case ReplayEventKind.Tick: AdvanceTime(); break;
                    case ReplayEventKind.AiTurn: PerformAiTurn(); break;
                }
            }
            finally { _applyingReplay = false; }
        }
        if (_replayPlayer.IsComplete)
        {
            ReplayMessage = "回放已结束。按 F6 可重新加载最近回放。";
            _replayPlayer = null;
            _replaying = false;
        }
    }

    public void SaveReplay()
    {
        if (_replayStore is null || _recorder is null) { ReplayMessage = "回放保存不可用。"; return; }
        ReplayMessage = _replayStore.TrySave(_recorder.Snapshot(), out var path)
            ? $"回放已保存：{Path.GetFileName(path)}"
            : "回放保存失败，请检查本地目录权限。";
    }

    public bool StartLatestReplay()
    {
        var document = _replayStore?.LoadLatest();
        if (document is null) { ReplayMessage = "没有可读取的回放文件。"; return false; }
        _replaying = true;
        _recorder = null;
        _replayPlayer = new ReplayPlayer(document);
        StartMode(document.Mode, document.Seed, startRecording: false);
        ReplayMessage = $"正在回放，速度 {_replayPlayer.Speed:0.##}×。F8 暂停，+/- 调速。";
        return true;
    }

    private void Record(ReplayEventKind kind, GameCommand command = GameCommand.None)
    {
        if (!_applyingReplay && !_replaying) _recorder?.Record(kind, command);
    }

    public bool PerformAiTurn()
    {
        if (!AiEnabled || Screen != GameScreen.Playing) return false;
        var target = Mode == GameMode.TwoPlayer ? SecondEngine : Engine;
        if (target is null || target.State.Status != GameStatus.Running) return false;

        try
        {
            var move = _aiPlanner.FindBestMove(target.State.Board, target.State.CurrentPiece);
            var changed = target.ApplyAiMove(move);
            if (changed)
            {
                Record(ReplayEventKind.AiTurn);
                ResolveAttack(target);
                UpdateScreenFromGameState();
                SaveHighScoreIfNeeded();
            }
            return changed;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private bool ControlsPlayerOneWithAi => AiEnabled && Mode != GameMode.TwoPlayer;
    private bool ControlsPlayerTwoWithAi => AiEnabled && Mode == GameMode.TwoPlayer;

    private void StartMode(GameMode mode, int? seed = null, bool startRecording = true)
    {
        _recordingSeed = seed ?? Random.Shared.Next();
        _garbageRandom = new Random(unchecked(_recordingSeed ^ 0x16a09e66));
        if (_providedInitialEngine is not null)
        {
            Engine = _providedInitialEngine;
            _providedInitialEngine = null;
        }
        else Engine = _engineFactory(_recordingSeed);
        Mode = mode;
        SecondEngine = mode == GameMode.TwoPlayer ? _engineFactory(unchecked(_recordingSeed ^ 0x5f3759df)) : null;
        AiEnabled = mode == GameMode.AiDemo;
        GameOverMessage = null;
        Screen = GameScreen.Playing;
        if (startRecording && !_replaying) _recorder = new ReplayRecorder(_recordingSeed, mode);
    }

    private void Restart()
    {
        Engine.Restart();
        SecondEngine = Mode == GameMode.TwoPlayer ? _engineFactory(unchecked(_recordingSeed ^ 0x5f3759df)) : null;
        AiEnabled = Mode == GameMode.AiDemo;
        GameOverMessage = null;
        Screen = GameScreen.Playing;
    }

    private void TogglePause()
    {
        if (Screen == GameScreen.Playing)
        {
            Screen = GameScreen.Paused;
            Engine.TogglePause();
            SecondEngine?.TogglePause();
        }
        else if (Screen == GameScreen.Paused)
        {
            Engine.TogglePause();
            SecondEngine?.TogglePause();
            Screen = GameScreen.Playing;
        }
    }

    private void ApplyPlayerCommand(GameCommand command)
    {
        var isPlayerTwoCommand = command is GameCommand.PlayerTwoLeft or GameCommand.PlayerTwoRight or
            GameCommand.PlayerTwoRotate or GameCommand.PlayerTwoSoftDrop or GameCommand.PlayerTwoHardDrop;
        var isPlayerOneCommand = command is GameCommand.PlayerOneLeft or GameCommand.PlayerOneRight or
            GameCommand.PlayerOneRotate or GameCommand.PlayerOneSoftDrop or GameCommand.PlayerOneHardDrop;

        GameEngine? target;
        if (Mode == GameMode.TwoPlayer)
        {
            if (isPlayerTwoCommand && !ControlsPlayerTwoWithAi) target = SecondEngine;
            else if (isPlayerOneCommand) target = Engine;
            else return;
        }
        else
        {
            target = Engine;
            if (!isPlayerOneCommand && !isPlayerTwoCommand) return;
        }

        if (target is null || target.State.Status != GameStatus.Running) return;
        if (isPlayerTwoCommand)
        {
            switch (command)
            {
                case GameCommand.PlayerTwoLeft: target.MoveHorizontal(-1); break;
                case GameCommand.PlayerTwoRight: target.MoveHorizontal(1); break;
                case GameCommand.PlayerTwoRotate: target.RotateClockwise(); break;
                case GameCommand.PlayerTwoSoftDrop: target.Tick(); break;
                case GameCommand.PlayerTwoHardDrop: target.HardDrop(); break;
            }
        }
        else
        {
            switch (command)
            {
                case GameCommand.PlayerOneLeft: target.MoveHorizontal(-1); break;
                case GameCommand.PlayerOneRight: target.MoveHorizontal(1); break;
                case GameCommand.PlayerOneRotate: target.RotateClockwise(); break;
                case GameCommand.PlayerOneSoftDrop: target.Tick(); break;
                case GameCommand.PlayerOneHardDrop: target.HardDrop(); break;
            }
        }

        ResolveAttack(target);
    }

    private void AdvanceEngine(GameEngine engine)
    {
        engine.Tick();
        ResolveAttack(engine);
    }

    private void ResolveAttack(GameEngine attacker)
    {
        var rows = attacker.ConsumeLastClearedRows();
        if (Mode != GameMode.TwoPlayer || rows < 2) return;
        var opponent = ReferenceEquals(attacker, Engine) ? SecondEngine : Engine;
        if (opponent is null) return;
        for (var i = 0; i < rows; i++)
            opponent.ReceiveGarbageRows(1, _garbageRandom.Next(opponent.State.Board.Width));
    }

    private void UpdateScreenFromGameState()
    {
        if (Screen != GameScreen.Playing) return;
        if (Mode == GameMode.TwoPlayer && SecondEngine is not null)
        {
            if (Engine.State.Status == GameStatus.GameOver && SecondEngine.State.Status == GameStatus.GameOver)
                GameOverMessage = "平局：双方棋盘都已结束。";
            else if (Engine.State.Status == GameStatus.GameOver)
                GameOverMessage = "玩家二获胜！";
            else if (SecondEngine.State.Status == GameStatus.GameOver)
                GameOverMessage = "玩家一获胜！";
            else return;
        }
        else if (Engine.State.Status == GameStatus.GameOver)
        {
            GameOverMessage = "游戏结束。";
        }
        else return;

        Screen = GameScreen.GameOver;
        SaveHighScoreIfNeeded();
    }

    private void SaveHighScoreIfNeeded()
    {
        var score = Math.Max(Engine.State.Score, SecondEngine?.State.Score ?? 0);
        if (score <= HighScore) return;
        HighScore = score;
        HighScoreSaveFailed = !_highScoreStore.Save(HighScore);
    }
}
