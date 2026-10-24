namespace Tetris.ConsoleApp;

public interface IInputSource
{
    bool TryRead(out ConsoleKey key);
}

public interface IConsoleKeyReader
{
    bool IsKeyAvailable { get; }
    ConsoleKey ReadKey();
}

public sealed class SystemConsoleKeyReader : IConsoleKeyReader
{
    public bool IsKeyAvailable => System.Console.KeyAvailable;
    public ConsoleKey ReadKey() => System.Console.ReadKey(intercept: true).Key;
}

public sealed class ConsoleInputSource : IInputSource
{
    private readonly IConsoleKeyReader _reader;

    public ConsoleInputSource(IConsoleKeyReader? reader = null) => _reader = reader ?? new SystemConsoleKeyReader();

    public bool TryRead(out ConsoleKey key)
    {
        key = default;
        try
        {
            if (!_reader.IsKeyAvailable) return false;
            key = _reader.ReadKey();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
