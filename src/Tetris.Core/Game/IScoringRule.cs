namespace Tetris.Core.Game;

/// <summary>可替换的消行计分规则。</summary>
public interface IScoringRule
{
    int CalculateScore(int clearedRows, int level);
}

public sealed class StandardScoringRule : IScoringRule
{
    public int CalculateScore(int clearedRows, int level) => checked(100 * clearedRows);
}
