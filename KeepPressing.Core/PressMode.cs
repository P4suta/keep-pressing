namespace KeepPressing.Core;

public abstract record PressMode
{
    private PressMode() { }

    /// <summary>Repeat at a fixed interval.</summary>
    public sealed record Repeat : PressMode
    {
        public Repeat(TimeSpan interval) => Interval = interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval must be positive.");

        public TimeSpan Interval { get; }
    }

    /// <summary>Hold down on start, release on stop.</summary>
    public sealed record Hold : PressMode
    {
        private Hold() { }

        public static Hold Instance { get; } = new();
    }
}
