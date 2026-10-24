using System.Xml.Linq;

namespace Tetris.Core.Persistence;

public sealed class HighScoreStore
{
    private readonly string _path;

    public HighScoreStore(string path) => _path = string.IsNullOrWhiteSpace(path)
        ? throw new ArgumentException("存档路径不能为空。", nameof(path))
        : path;

    public int Load()
    {
        try
        {
            if (!File.Exists(_path)) return 0;
            var root = XDocument.Load(_path).Root;
            if (root?.Name != "HighScore") return 0;
            return int.TryParse((string?)root.Attribute("score"), out var score) && score >= 0 ? score : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Xml.XmlException or ArgumentException)
        {
            return 0;
        }
    }

    public bool Save(int score)
    {
        if (score < 0) return false;
        var tempPath = _path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
            if (string.IsNullOrEmpty(directory)) return false;
            Directory.CreateDirectory(directory);
            var document = new XDocument(new XElement("HighScore", new XAttribute("score", score)));
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                document.Save(stream);
            File.Move(tempPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
