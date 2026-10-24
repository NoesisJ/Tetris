using System.Text.Json;

namespace Tetris.ConsoleApp;

public sealed class ReplayStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public bool TrySave(ReplayDocument document, out string? savedPath)
    {
        savedPath = null;
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"replay-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(document, Options));
            savedPath = path;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return false; }
    }

    public ReplayDocument? LoadLatest()
    {
        try
        {
            if (!Directory.Exists(directory)) return null;
            foreach (var path in Directory.EnumerateFiles(directory, "replay-*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try
                {
                    var document = JsonSerializer.Deserialize<ReplayDocument>(File.ReadAllText(path));
                    if (document is not null) { _ = new ReplayPlayer(document); return document; }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NullReferenceException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        return null;
    }
}
