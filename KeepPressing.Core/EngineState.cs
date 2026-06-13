namespace KeepPressing.Core;

/// <summary>
/// エンジンの状態。Idle / Running の 2 状態のみの閉じた代数的データ型。
/// </summary>
public abstract record EngineState
{
    private EngineState() { }

    /// <summary>停止中。</summary>
    public sealed record Idle : EngineState
    {
        private Idle() { }

        public static Idle Instance { get; } = new();
    }

    /// <summary>実行中。実行内容のスナップショットを保持する。</summary>
    public sealed record Running(PressSpec Spec) : EngineState;
}
