using Microsoft.Data.Sqlite;

namespace Tetris.Core.Persistence;

/// <summary>本地 SQLite 历史成绩库；每次操作独立建连，失败时返回错误而不影响游戏循环。</summary>
public sealed class SqliteScoreHistoryStore : IScoreHistoryStore
{
    private readonly string _connectionString;

    public SqliteScoreHistoryStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("数据库路径不能为空。", nameof(databasePath));
        var fullPath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
            DefaultTimeout = 3
        }.ToString();
    }

    public bool TryAddResults(IReadOnlyCollection<NewScoreEntry> results, out string? errorMessage)
    {
        errorMessage = null;
        if (results is null || results.Count == 0) return true;
        if (results.Any(result => result is null || string.IsNullOrWhiteSpace(result.PlayerName) || result.Score < 0 || result.TotalLines < 0 || string.IsNullOrWhiteSpace(result.GameMode)))
        {
            errorMessage = "成绩数据无效，未写入历史记录。";
            return false;
        }

        try
        {
            EnsureDirectory();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO ScoreHistory (PlayerName, Score, TotalLines, GameMode, PlayedAtUtc) VALUES ($name, $score, $lines, $mode, $playedAt);";
            var name = command.Parameters.Add("$name", SqliteType.Text);
            var score = command.Parameters.Add("$score", SqliteType.Integer);
            var lines = command.Parameters.Add("$lines", SqliteType.Integer);
            var mode = command.Parameters.Add("$mode", SqliteType.Text);
            var playedAt = command.Parameters.Add("$playedAt", SqliteType.Text);
            var timestamp = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var result in results)
            {
                name.Value = result.PlayerName;
                score.Value = result.Score;
                lines.Value = result.TotalLines;
                mode.Value = result.GameMode;
                playedAt.Value = timestamp;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
            return true;
        }
        catch (Exception ex) when (IsDatabaseOrIoError(ex))
        {
            errorMessage = "历史成绩保存失败：" + Describe(ex);
            return false;
        }
    }

    public IReadOnlyList<ScoreEntry> GetTopScores(int limit, out string? errorMessage)
    {
        errorMessage = null;
        if (limit is < 1 or > 100)
        {
            errorMessage = "排行榜数量必须在 1 到 100 之间。";
            return [];
        }

        try
        {
            EnsureDirectory();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            EnsureSchema(connection);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, PlayerName, Score, TotalLines, GameMode, PlayedAtUtc FROM ScoreHistory ORDER BY Score DESC, PlayedAtUtc ASC, Id ASC LIMIT $limit;";
            command.Parameters.AddWithValue("$limit", limit);
            using var reader = command.ExecuteReader();
            var scores = new List<ScoreEntry>();
            while (reader.Read())
            {
                scores.Add(new ScoreEntry(
                    reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetString(4),
                    DateTime.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)));
            }
            return scores;
        }
        catch (Exception ex) when (IsDatabaseOrIoError(ex))
        {
            errorMessage = "排行榜读取失败：" + Describe(ex);
            return [];
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ScoreHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayerName TEXT NOT NULL,
                Score INTEGER NOT NULL CHECK (Score >= 0),
                TotalLines INTEGER NOT NULL CHECK (TotalLines >= 0),
                GameMode TEXT NOT NULL,
                PlayedAtUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ScoreHistory_Leaderboard
                ON ScoreHistory (Score DESC, PlayedAtUtc ASC, Id ASC);
            """;
        command.ExecuteNonQuery();
    }

    private static bool IsDatabaseOrIoError(Exception exception) => exception is
        SqliteException or IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException or FormatException;

    private static string Describe(Exception exception) => exception is SqliteException
        ? "数据库文件不可用或格式损坏。"
        : "数据库目录不可用或无写入权限。";
}
