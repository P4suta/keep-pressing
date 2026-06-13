namespace KeepPressing.Core;

/// <summary>
/// 入力合成のポート。Core が持つ唯一の副作用境界であり、
/// 実装（Win32 SendInput / テスト用 Fake）はこのインターフェイスの背後に隔離される。
/// </summary>
public interface IInputSynthesizer
{
    /// <summary>対象を押下する（Down）。</summary>
    void Press(InputTarget target);

    /// <summary>対象を解放する（Up）。押されていない対象への解放は無害な no-op であること。</summary>
    void Release(InputTarget target);

    /// <summary>1 打（Down → Up）。実装は 2 イベントの原子的なバッチ送出に override してよい。</summary>
    void Tap(InputTarget target)
    {
        Press(target);
        Release(target);
    }
}
