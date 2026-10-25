using Tetris.Core.Persistence;

namespace Tetris.Tests;

public sealed class SqliteScoreHistoryStoreTests
{
    [Fact]
    public void AddResults_PersistsMultipleScoresAndReturnsLeaderboardOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-scores-{Guid.NewGuid():N}");
        try
        {
            var store = new SqliteScoreHistoryStore(Path.Combine(directory, "scores.db"));
            Assert.True(store.TryAddResults(
            [
                new("玩家一", 400, 4, "双人对战"),
                new("AI", 900, 9, "AI 观战"),
                new("玩家一", 900, 10, "单人游戏")
            ], out var saveError));
            Assert.Null(saveError);

            var scores = store.GetTopScores(2, out var queryError);

            Assert.Null(queryError);
            Assert.Equal(2, scores.Count);
            Assert.Equal(900, scores[0].Score);
            Assert.Equal("AI", scores[0].PlayerName);
            Assert.Equal(900, scores[1].Score);
            Assert.Equal(3, store.GetTopScores(10, out _).Count);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Store_ReportsCorruptDatabaseAndInvalidInputWithoutThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"tetris-scores-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "broken.db");
            File.WriteAllText(path, "not a sqlite database");
            var store = new SqliteScoreHistoryStore(path);

            var scores = store.GetTopScores(10, out var queryError);

            Assert.Empty(scores);
            Assert.NotNull(queryError);
            Assert.False(store.TryAddResults([new("", -1, -1, "")], out var saveError));
            Assert.NotNull(saveError);
            Assert.Empty(store.GetTopScores(0, out var limitError));
            Assert.NotNull(limitError);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
