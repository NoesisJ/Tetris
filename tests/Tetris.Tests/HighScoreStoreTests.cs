using Tetris.Core.Persistence;

namespace Tetris.Tests;

public sealed class HighScoreStoreTests
{
    [Fact]
    public void Load_ReturnsZeroWhenFileIsMissingOrCorrupt()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "highscore.xml");
            var store = new HighScoreStore(path);
            Assert.Equal(0, store.Load());
            File.WriteAllText(path, "<HighScore score=\"broken\">");
            Assert.Equal(0, store.Load());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void SaveAndLoad_RoundTripScoreAsXml()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "nested", "highscore.xml");
            var store = new HighScoreStore(path);
            Assert.True(store.Save(12345));
            Assert.Equal(12345, store.Load());
            Assert.Contains("<HighScore", File.ReadAllText(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Save_ReturnsFalseForInvalidScoreOrDestination()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new HighScoreStore(Path.Combine(directory, "existing-directory"));
            Directory.CreateDirectory(Path.Combine(directory, "existing-directory"));
            Assert.False(store.Save(-1));
            Assert.False(store.Save(10));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tetris-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
