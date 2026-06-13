namespace KeepPressing.Core;

/// <summary>
/// 押下モード。private コンストラクタで閉じた代数的データ型。
/// 「長押しなのに間隔を持つ」ような不正状態は型レベルで表現できない。
/// </summary>
public abstract record PressMode
{
    private PressMode() { }

    /// <summary>一定間隔での連打。</summary>
    public sealed record Repeat : PressMode
    {
        public Repeat(TimeSpan interval) => Interval = interval > TimeSpan.Zero
            ? interval
            : throw new ArgumentOutOfRangeException(nameof(interval), interval, "連打間隔は正の値でなければならない。");

        public TimeSpan Interval { get; }
    }

    /// <summary>長押し（開始で押下、停止で解放）。</summary>
    public sealed record Hold : PressMode
    {
        private Hold() { }

        public static Hold Instance { get; } = new();
    }
}
