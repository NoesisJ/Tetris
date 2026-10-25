namespace Tetris.Core.Persistence;

public sealed record ScoreEntry(
    long Id,
    string PlayerName,
    int Score,
    int TotalLines,
    string GameMode,
    DateTime PlayedAtUtc);

public sealed record NewScoreEntry(string PlayerName, int Score, int TotalLines, string GameMode);
