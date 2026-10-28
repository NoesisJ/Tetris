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
        foreach (var entry in ListReplays(out _))
        {
            var document = Load(entry.Path);
            if (document is not null) return document;
        }
        return null;
    }

    public IReadOnlyList<ReplayEntry> ListReplays(out string? errorMessage)
    {
        errorMessage = null;
        var entries = new List<ReplayEntry>();
        var skipped = 0;
        try
        {
            // Missing folders are normal on first launch; other IO errors should be visible.
            foreach (var path in Directory.EnumerateFiles(directory, "replay-*.json"))
            {
                var document = Load(path);
                if (document is null) { skipped++; continue; }
                try
                {
                    entries.Add(new ReplayEntry(path, File.GetLastWriteTimeUtc(path), document.Mode,
                        document.Events.Count == 0 ? 0 : document.Events[^1].TimeMilliseconds));
                }
                catch (Exception ex) when (IsReadError(ex)) { skipped++; }
            }
        }
        catch (DirectoryNotFoundException) { }
        catch (Exception ex) when (IsReadError(ex))
        {
            errorMessage = "录像目录无法读取，请检查目录权限或路径。";
        }
        if (skipped > 0) errorMessage ??= $"已跳过 {skipped} 个损坏或无法读取的录像文件。";
        return entries.OrderByDescending(entry => entry.SavedAtUtc)
            .ThenBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
    }

    public ReplayDocument? Load(string path)
    {
        try
        {
            var document = JsonSerializer.Deserialize<ReplayDocument>(File.ReadAllText(path));
            if (document is not null) _ = new ReplayPlayer(document);
            return document;
        }
        catch (Exception ex) when (IsReadError(ex)) { return null; }
    }

    private static bool IsReadError(Exception ex) => ex is IOException or UnauthorizedAccessException or
        System.Security.SecurityException or JsonException or InvalidDataException or ArgumentException or NotSupportedException;
}
