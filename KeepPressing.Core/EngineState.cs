namespace KeepPressing.Core;

public abstract record EngineState
{
    private EngineState() { }

    public sealed record Idle : EngineState
    {
        private Idle() { }

        public static Idle Instance { get; } = new();
    }

    /// <summary>Running, holding a snapshot of what is being pressed.</summary>
    public sealed record Running(PressSpec Spec) : EngineState;
}
