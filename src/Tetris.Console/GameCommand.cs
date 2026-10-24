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
    Restart,
    MainMenu,
    Quit,
    SaveReplay,
    PlayReplay,
    ToggleReplayPause,
    ReplayFaster,
    ReplaySlower
}

public enum GameScreen
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

public enum GameMode
{
    SinglePlayer,
    TwoPlayer,
    AiDemo
}
