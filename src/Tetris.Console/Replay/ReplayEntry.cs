namespace Tetris.ConsoleApp;

public sealed record ReplayEntry(string Path, DateTime SavedAtUtc, GameMode Mode, double DurationMilliseconds);
