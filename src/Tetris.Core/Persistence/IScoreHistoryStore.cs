namespace Tetris.Core.Persistence;

public interface IScoreHistoryStore
{
    bool TryAddResults(IReadOnlyCollection<NewScoreEntry> results, out string? errorMessage);
    IReadOnlyList<ScoreEntry> GetTopScores(int limit, out string? errorMessage);
}
