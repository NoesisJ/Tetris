using System.Text;
using Tetris.Core.Domain;

namespace Tetris.ConsoleApp;

public sealed class GameViewFormatter
{
    public const int MinimumWidth = 80;
    public const int MinimumTwoPlayerWidth = 110;
    public const int MinimumHeight = 23;

    public string Format(GameController controller, int terminalWidth, int terminalHeight)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var requiredWidth = controller.Screen != GameScreen.MainMenu && controller.Mode == GameMode.TwoPlayer
            ? MinimumTwoPlayerWidth
            : MinimumWidth;
        if (terminalWidth < requiredWidth || terminalHeight < MinimumHeight)
            return $"请将终端窗口调整到至少 {requiredWidth} 列 × {MinimumHeight} 行。";

        if (controller.Screen == GameScreen.MainMenu)
        {
            var menu = MenuLines();
            if (controller.ReplayMessage is not null) menu.Add(controller.ReplayMessage);
            if (controller.ScoreHistoryError is not null) menu.Add(controller.ScoreHistoryError);
            return FitLines(menu, terminalWidth);
        }

        if (controller.Screen == GameScreen.Leaderboard)
            return FitLines(LeaderboardLines(controller), terminalWidth);

        var lines = controller.Mode == GameMode.TwoPlayer && controller.SecondEngine is not null
            ? TwoPlayerLines(controller)
            : SinglePlayerLines(controller);

        if (controller.Screen == GameScreen.Paused)
        {
            lines.Add(string.Empty);
            lines.Add("游戏已暂停。按 P 继续，按 Esc 返回主菜单。");
        }
        else if (controller.Screen == GameScreen.GameOver)
        {
            lines.Add(string.Empty);
            lines.Add(controller.GameOverMessage ?? "游戏结束。");
            lines.Add("按 R 再来一局，按 Esc 返回主菜单，按 Q 退出。");
        }

        if (controller.HighScoreSaveFailed) lines.Add("提示：高分存档不可写，本局仍可继续。");
        if (controller.ReplayMessage is not null) lines.Add(controller.ReplayMessage);
        if (controller.ScoreHistoryError is not null) lines.Add(controller.ScoreHistoryError);
        return FitLines(lines, terminalWidth);
    }

    public string Format(GameState state, int highScore, bool aiEnabled, bool highScoreSaveFailed, int terminalWidth, int terminalHeight)
    {
        if (terminalWidth < MinimumWidth || terminalHeight < MinimumHeight)
            return $"请将终端窗口调整到至少 {MinimumWidth} 列 × {MinimumHeight} 行。";

        var lines = new List<string> { "TETRIS 12 × 16", BoardHeader(state.Board.Width, "下一个：") };
        AppendBoardRows(lines, state.Board, state.CurrentPiece, "│", "│");
        lines.Add(BoardFooter(state.Board.Width));
        lines.Add(ScoreLine("玩家一", state, highScore));
        lines.Add($"状态：{StatusText(state.Status)}   AI：{(aiEnabled ? "开启" : "关闭")}");
        if (highScoreSaveFailed) lines.Add("提示：高分存档不可写，本局仍可继续。");
        lines.Add("W/A/S/D 移动和旋转  空格 硬降  P 暂停  M AI  R 重开  Esc 菜单  Q 退出");
        return FitLines(lines, terminalWidth);
    }

    private static List<string> MenuLines() =>
    [
        "TETRIS 俄罗斯方块",
        "",
        "选择游戏模式：",
        "1  单人游戏",
        "2  双人本地对战（棋盘并排，消除多行向对手发送垃圾行）",
        "3  AI 观战演示",
        "4  排行榜",
        "",
        "玩家一：A/D 左右移动，W 旋转，S 加速下落，空格硬降。",
        "玩家二：←/→ 左右移动，↑ 旋转，↓ 加速下落，Enter 硬降。",
        "游戏中按 P 暂停/继续，M 切换双人模式中的 AI，R 重开，Esc 返回菜单，Q 退出。",
        "AI 观战时按 F9 切换普通/2 倍速；F5 保存回放，F6 播放，F8 暂停，+/- 调速。"
    ];

    private static List<string> LeaderboardLines(GameController controller)
    {
        var lines = new List<string> { "俄罗斯方块 - 历史排行榜", "", "排名  玩家      分数    消行  模式          时间（本地）" };
        if (controller.Leaderboard.Count == 0 && controller.ScoreHistoryError is null)
            lines.Add("暂无已完成对局记录。");
        for (var i = 0; i < controller.Leaderboard.Count; i++)
        {
            var entry = controller.Leaderboard[i];
            lines.Add($"{i + 1,2}    {entry.PlayerName,-8}  {entry.Score,6}  {entry.TotalLines,4}  {entry.GameMode,-10}  {entry.PlayedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
        }
        if (controller.ScoreHistoryError is not null) lines.Add(controller.ScoreHistoryError);
        lines.Add("");
        lines.Add("按 Esc 返回主菜单。");
        return lines;
    }

    private static List<string> SinglePlayerLines(GameController controller)
    {
        var engine = controller.Engine;
        var mode = controller.Mode == GameMode.AiDemo ? "AI 观战演示" : "单人游戏";
        var lines = new List<string> { $"TETRIS 12 × 16   模式：{mode}", BoardHeader(engine.State.Board.Width, "下一个：") };
        AppendBoardRows(lines, engine.State.Board, engine.State.CurrentPiece, "│", "│", engine.State.NextPiece);
        lines.Add(BoardFooter(engine.State.Board.Width));
        lines.Add(ScoreLine("玩家一", engine.State, controller.HighScore));
        if (controller.Mode == GameMode.AiDemo)
        {
            lines.Add($"状态：{StatusText(engine.State.Status)}   AI：自动演示   观战速度：{(controller.AiDemoSpeedMultiplier == 1 ? "普通" : "2 倍速")}");
            lines.Add("AI 自动操作中；F9 切换普通/2 倍速，P 暂停/继续，R 重开，Esc 返回菜单，Q 退出。");
        }
        else
        {
            lines.Add($"状态：{StatusText(engine.State.Status)}   AI：{(controller.AiEnabled ? "开启" : "关闭")}   操作：A/D 移动，W 旋转，S 加速，空格硬降");
            lines.Add("P 暂停/继续  M 切换 AI  R 重开  Esc 返回菜单  Q 退出");
        }
        if (controller.ReplayMessage is not null) lines.Add(controller.ReplayMessage);
        return lines;
    }

    private static List<string> TwoPlayerLines(GameController controller)
    {
        var first = controller.Engine.State;
        var second = controller.SecondEngine!.State;
        var lines = new List<string>
        {
            $"TETRIS 双人对战   {(controller.AiEnabled ? "玩家一 vs AI" : "玩家一 vs 玩家二")}",
            $"玩家一  分数 {first.Score}  等级 {first.Level}  消行 {first.TotalLines}              玩家二  分数 {second.Score}  等级 {second.Level}  消行 {second.TotalLines}"
        };
        var firstCells = first.CurrentPiece.GetCells().ToHashSet();
        var secondCells = second.CurrentPiece.GetCells().ToHashSet();
        lines.Add("┌" + new string('─', first.Board.Width * 2) + "┐     ┌" + new string('─', second.Board.Width * 2) + "┐");
        for (var y = 0; y < first.Board.Height; y++)
        {
            var rowOne = BoardRow(first.Board, firstCells, y);
            var rowTwo = BoardRow(second.Board, secondCells, y);
            lines.Add($"{rowOne}     {rowTwo}");
        }
        lines.Add("└" + new string('─', first.Board.Width * 2) + "┘     └" + new string('─', second.Board.Width * 2) + "┘");
        lines.Add($"玩家一下一个：{PiecePreview(first.NextPiece)}        玩家二下一个：{PiecePreview(second.NextPiece)}        最高分 {controller.HighScore}");
        lines.Add("玩家一：A/D 移动，W 旋转，S 加速，空格硬降。玩家二：←/→ 移动，↑ 旋转，↓ 加速，Enter 硬降。");
        lines.Add("一次消除 2 行或以上会向对手增加等量垃圾行。P 暂停/继续，M 切换玩家二 AI，R 重开，Esc 菜单，Q 退出。");
        if (controller.ReplayMessage is not null) lines.Add(controller.ReplayMessage);
        return lines;
    }

    private static string BoardHeader(int width, string suffix) => "┌" + new string('─', width * 2) + "┐   " + suffix;
    private static string BoardFooter(int width) => "└" + new string('─', width * 2) + "┘";

    private static void AppendBoardRows(List<string> lines, Board board, Piece current, string left, string right, Piece? preview = null)
    {
        var currentCells = current.GetCells().ToHashSet();
        for (var y = 0; y < board.Height; y++)
        {
            var row = new StringBuilder(left);
            for (var x = 0; x < board.Width; x++)
                row.Append(board.GetCell(x, y).HasValue || currentCells.Contains(new GridPoint(x, y)) ? "██" : "  ");
            row.Append(right);
            if (preview.HasValue)
                row.Append("   ").Append(PiecePreview(preview.Value, y - 1));
            lines.Add(row.ToString());
        }
    }

    private static string BoardRow(Board board, HashSet<GridPoint> activeCells, int y)
    {
        var row = new StringBuilder("│");
        for (var x = 0; x < board.Width; x++)
            row.Append(board.GetCell(x, y).HasValue || activeCells.Contains(new GridPoint(x, y)) ? "██" : "  ");
        return row.Append('│').ToString();
    }

    private static string PiecePreview(Piece piece, int row = 0)
    {
        var cells = piece.GetRelativeCells();
        var output = new StringBuilder();
        for (var x = 0; x < 4; x++) output.Append(cells.Any(cell => cell.X == x && cell.Y == row) ? "██" : "  ");
        return output.ToString();
    }

    private static string ScoreLine(string label, GameState state, int highScore) =>
        $"{label}分数 {state.Score}   最高分 {highScore}   等级 {state.Level}   已消行 {state.TotalLines}";

    private static string StatusText(GameStatus status) => status switch
    {
        GameStatus.Running => "进行中",
        GameStatus.Paused => "已暂停",
        _ => "游戏结束"
    };

    private static string FitLines(IEnumerable<string> lines, int terminalWidth)
    {
        var lineWidth = Math.Max(1, terminalWidth - 1);
        return string.Join(Environment.NewLine, lines.Select(line => FitConsoleLine(line, lineWidth)));
    }

    private static string FitConsoleLine(string text, int columnLimit)
    {
        var output = new StringBuilder();
        var columns = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var width = IsWide(rune.Value) ? 2 : 1;
            if (columns + width > columnLimit) break;
            output.Append(rune);
            columns += width;
        }
        return output.Append(' ', columnLimit - columns).ToString();
    }

    private static bool IsWide(int value) =>
        value is >= 0x1100 and <= 0x11FF or >= 0x2E80 and <= 0xA4CF or >= 0xAC00 and <= 0xD7AF or
            >= 0xF900 and <= 0xFAFF or >= 0xFE30 and <= 0xFE6F or >= 0xFF00 and <= 0xFF60 or
            >= 0xFFE0 and <= 0xFFE6 or >= 0x1F300 and <= 0x1FAFF or >= 0x20000 and <= 0x3FFFD;
}
