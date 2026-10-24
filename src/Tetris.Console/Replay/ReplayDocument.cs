namespace Tetris.ConsoleApp;

public enum ReplayEventKind { Command, Tick, AiTurn }

public sealed record ReplayEvent(double TimeMilliseconds, ReplayEventKind Kind, GameCommand Command = GameCommand.None);

public sealed record ReplayDocument(int Version, int Seed, GameMode Mode, IReadOnlyList<ReplayEvent> Events);

public sealed class ReplayRecorder(int seed, GameMode mode)
{
    private readonly List<ReplayEvent> _events = [];
    public double ElapsedMilliseconds { get; private set; }
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed > TimeSpan.Zero) ElapsedMilliseconds += elapsed.TotalMilliseconds;
    }
    public void Record(ReplayEventKind kind, GameCommand command = GameCommand.None) =>
        _events.Add(new ReplayEvent(ElapsedMilliseconds, kind, command));
    public ReplayDocument Snapshot() => new(1, seed, mode, _events.ToArray());
}

public sealed class ReplayPlayer
{
    private readonly ReplayDocument _document;
    private int _nextEvent;
    public ReplayPlayer(ReplayDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        if (document.Version != 1 || !Enum.IsDefined(document.Mode) || document.Events is null ||
            document.Events.Any(e => e is null || e.TimeMilliseconds < 0 || !double.IsFinite(e.TimeMilliseconds) ||
                !Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.Command)) ||
            !document.Events.SequenceEqual(document.Events.OrderBy(e => e.TimeMilliseconds)))
            throw new InvalidDataException("回放文件版本或事件序列无效。");
    }
    public double PositionMilliseconds { get; private set; }
    public double Speed { get; private set; } = 1;
    public bool IsPaused { get; private set; }
    public bool IsComplete => _nextEvent >= _document.Events.Count;
    public void TogglePause() => IsPaused = !IsPaused;
    public void SetSpeed(double speed) => Speed = Math.Clamp(speed, 0.25, 4.0);
    public IReadOnlyList<ReplayEvent> Advance(TimeSpan elapsed)
    {
        if (!IsPaused && elapsed > TimeSpan.Zero) PositionMilliseconds += elapsed.TotalMilliseconds * Speed;
        var due = new List<ReplayEvent>();
        while (_nextEvent < _document.Events.Count && _document.Events[_nextEvent].TimeMilliseconds <= PositionMilliseconds)
            due.Add(_document.Events[_nextEvent++]);
        return due;
    }
}
