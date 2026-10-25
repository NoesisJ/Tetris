using System.Diagnostics;
using Tetris.ConsoleApp;
using Tetris.Core.Game;
using Tetris.Core.Persistence;

if (System.Console.IsInputRedirected)
{
    System.Console.WriteLine("俄罗斯方块需要交互式终端，请在 VS Code 集成终端或 Windows Terminal 中运行。");
    return;
}

var saveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tetris");
var store = new HighScoreStore(Path.Combine(saveDirectory, "highscore.xml"));
var replayStore = new ReplayStore(Path.Combine(saveDirectory, "replays"));
var scoreHistoryStore = new SqliteScoreHistoryStore(Path.Combine(saveDirectory, "scores.db"));
var controller = new GameController(store, seed => new GameEngine(new RandomPieceSource(seed)), replayStore, scoreHistoryStore);
var input = new ConsoleInputSource();
var renderer = new ConsoleGameRenderer();
var clock = Stopwatch.StartNew();
var previous = clock.Elapsed;
var dropAccumulator = TimeSpan.Zero;
var aiAccumulator = TimeSpan.Zero;
var renderAccumulator = TimeSpan.Zero;

try
{
    System.Console.CursorVisible = false;
    while (!controller.IsQuitRequested)
    {
        while (input.TryRead(out var key))
        {
            var previousAi = controller.AiEnabled;
            controller.Handle(GameInputMapper.Map(key));
            if (previousAi != controller.AiEnabled) aiAccumulator = TimeSpan.Zero;
        }

        var now = clock.Elapsed;
        var elapsed = now - previous;
        previous = now;
        renderAccumulator += elapsed;
        controller.AdvanceReplay(elapsed);

        if (!controller.IsReplaying && controller.Screen == GameScreen.Playing)
        {
            var gameElapsed = controller.Mode == GameMode.AiDemo
                ? TimeSpan.FromTicks(elapsed.Ticks * controller.AiDemoSpeedMultiplier)
                : elapsed;
            dropAccumulator += gameElapsed;
            if (controller.AiEnabled)
            {
                aiAccumulator += gameElapsed;
                if (aiAccumulator.TotalMilliseconds >= 250)
                {
                    controller.PerformAiTurn();
                    aiAccumulator = TimeSpan.Zero;
                }
            }

            var dropInterval = controller.Engine.DropIntervalMilliseconds;
            while (dropAccumulator.TotalMilliseconds >= dropInterval)
            {
                controller.AdvanceTime();
                dropAccumulator -= TimeSpan.FromMilliseconds(dropInterval);
                if (controller.Screen != GameScreen.Playing) break;
            }
        }
        else
        {
            dropAccumulator = TimeSpan.Zero;
            aiAccumulator = TimeSpan.Zero;
        }

        if (renderAccumulator.TotalMilliseconds >= 100)
        {
            renderer.Render(controller);
            renderAccumulator = TimeSpan.Zero;
        }
        Thread.Sleep(20);
    }
}
finally
{
    try { System.Console.CursorVisible = true; }
    catch (IOException) { }
}
