namespace Tetris.ConsoleApp;

public interface ITerminalSurface
{
    int Width { get; }
    int Height { get; }
    void Clear();
    void SetCursorPosition(int left, int top);
    void Write(string text);
}

public sealed class SystemTerminalSurface : ITerminalSurface
{
    public int Width => System.Console.WindowWidth;
    public int Height => System.Console.WindowHeight;
    public void Clear() => System.Console.Clear();
    public void SetCursorPosition(int left, int top) => System.Console.SetCursorPosition(left, top);
    public void Write(string text) => System.Console.Write(text);
}

public sealed class ConsoleGameRenderer : IGameRenderer
{
    private readonly GameViewFormatter _formatter = new();
    private readonly ITerminalSurface _terminal;
    private GameScreen? _previousScreen;
    private GameMode? _previousMode;

    public ConsoleGameRenderer(ITerminalSurface? terminal = null) => _terminal = terminal ?? new SystemTerminalSurface();

    public void Render(GameController controller)
    {
        try
        {
            var width = _terminal.Width;
            var height = _terminal.Height;
            var frame = _formatter.Format(controller, width, height);
            var screenChanged = _previousScreen != controller.Screen || _previousMode != controller.Mode;
            _previousScreen = controller.Screen;
            _previousMode = controller.Mode;
            var minimumWidth = controller.Screen != GameScreen.MainMenu && controller.Mode == GameMode.TwoPlayer
                ? GameViewFormatter.MinimumTwoPlayerWidth
                : GameViewFormatter.MinimumWidth;
            if (width < minimumWidth || height < GameViewFormatter.MinimumHeight)
            {
                _terminal.Clear();
                _terminal.Write(frame);
                return;
            }
            if (screenChanged) _terminal.Clear();
            _terminal.SetCursorPosition(0, 0);
            _terminal.Write(frame);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            // 终端被缩放、关闭或重定向时，忽略这一帧绘制错误，游戏状态仍由核心逻辑维护。
        }
    }

    public void Render(Tetris.Core.Domain.GameState state, int highScore, bool aiEnabled, bool highScoreSaveFailed)
    {
        try
        {
            var width = _terminal.Width;
            var height = _terminal.Height;
            var frame = _formatter.Format(state, highScore, aiEnabled, highScoreSaveFailed, width, height);
            if (width < GameViewFormatter.MinimumWidth || height < GameViewFormatter.MinimumHeight)
            {
                _terminal.Clear();
                _terminal.Write(frame);
                return;
            }
            _terminal.SetCursorPosition(0, 0);
            _terminal.Write(frame);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or InvalidOperationException) { }
    }
}
