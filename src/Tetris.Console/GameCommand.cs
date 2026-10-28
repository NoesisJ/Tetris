namespace Tetris.ConsoleApp;

public enum GameCommand
{
    None,
    StartSinglePlayer,
    StartTwoPlayer,
    StartAiDemo,
    PlayerOneLeft,
    PlayerOneRight,
    PlayerOneRotate,
    PlayerOneSoftDrop,
    PlayerOneHardDrop,
    PlayerTwoLeft,
    PlayerTwoRight,
    PlayerTwoRotate,
    PlayerTwoSoftDrop,
    PlayerTwoHardDrop,
    TogglePause,
    ToggleAi,
    ToggleAiDemoSpeed,
    Restart,
    MainMenu,
    Quit,
    SaveReplay,
    PlayReplay,
    ToggleReplayPause,
    ReplayFaster,
    ReplaySlower,
    ShowLeaderboard,
    ReplayPageUp,
    ReplayPageDown
}

public enum GameScreen
{
    MainMenu,
    Playing,
    Paused,
    GameOver,
    Leaderboard,
    ReplayLibrary
}

public enum GameMode
{
    SinglePlayer,
    TwoPlayer,
    AiDemo
}
