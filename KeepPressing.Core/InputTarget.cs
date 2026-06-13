namespace KeepPressing.Core;

/// <summary>連打対象のマウスボタン。</summary>
public enum MouseButton
{
    Left,
    Right,
    Middle,
}

/// <summary>
/// 入力対象。private コンストラクタで閉じた代数的データ型であり、
/// 派生は <see cref="Mouse"/> と <see cref="Key"/> のみ — switch は網羅的に書ける。
/// </summary>
public abstract record InputTarget
{
    private InputTarget() { }

    /// <summary>マウスボタン。<paramref name="Position"/> が null なら現在のカーソル位置で押下する。</summary>
    public sealed record Mouse(MouseButton Button, ScreenPoint? Position = null) : InputTarget;

    /// <summary>キーボードのキー。</summary>
    public sealed record Key(KeyCode Code) : InputTarget;
}
