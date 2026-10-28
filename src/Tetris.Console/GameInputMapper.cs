namespace Tetris.ConsoleApp;

public static class GameInputMapper
{
    public static GameCommand Map(ConsoleKey key) => key switch
    {
        ConsoleKey.D1 or ConsoleKey.NumPad1 => GameCommand.StartSinglePlayer,
        ConsoleKey.D2 or ConsoleKey.NumPad2 => GameCommand.StartTwoPlayer,
        ConsoleKey.D3 or ConsoleKey.NumPad3 => GameCommand.StartAiDemo,
        ConsoleKey.D4 or ConsoleKey.NumPad4 => GameCommand.ShowLeaderboard,
        ConsoleKey.D5 or ConsoleKey.NumPad5 => GameCommand.PlayReplay,
        ConsoleKey.A => GameCommand.PlayerOneLeft,
        ConsoleKey.D => GameCommand.PlayerOneRight,
        ConsoleKey.W => GameCommand.PlayerOneRotate,
        ConsoleKey.S => GameCommand.PlayerOneSoftDrop,
        ConsoleKey.Spacebar => GameCommand.PlayerOneHardDrop,
        ConsoleKey.LeftArrow => GameCommand.PlayerTwoLeft,
        ConsoleKey.RightArrow => GameCommand.PlayerTwoRight,
        ConsoleKey.UpArrow => GameCommand.PlayerTwoRotate,
        ConsoleKey.DownArrow => GameCommand.PlayerTwoSoftDrop,
        ConsoleKey.Enter => GameCommand.PlayerTwoHardDrop,
        ConsoleKey.P => GameCommand.TogglePause,
        ConsoleKey.M => GameCommand.ToggleAi,
        ConsoleKey.F9 => GameCommand.ToggleAiDemoSpeed,
        ConsoleKey.R => GameCommand.Restart,
        ConsoleKey.Escape => GameCommand.MainMenu,
        ConsoleKey.Q => GameCommand.Quit,
        ConsoleKey.F5 => GameCommand.SaveReplay,
        ConsoleKey.F6 => GameCommand.PlayReplay,
        ConsoleKey.PageUp => GameCommand.ReplayPageUp,
        ConsoleKey.PageDown => GameCommand.ReplayPageDown,
        ConsoleKey.F8 => GameCommand.ToggleReplayPause,
        ConsoleKey.OemPlus or ConsoleKey.Add => GameCommand.ReplayFaster,
        ConsoleKey.OemMinus or ConsoleKey.Subtract => GameCommand.ReplaySlower,
        _ => GameCommand.None
    };
}
