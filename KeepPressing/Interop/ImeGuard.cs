using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace KeepPressing.Interop;

/// <summary>
/// ウィンドウの IME 関連付けの一時無効化。
/// キー設定モード中に IME（日本語入力など）が文字キーを composition として
/// 取り込んでしまい、KeyDown がアプリへ届かない問題を防ぐ。
/// </summary>
public interface IImeGuard
{
    /// <summary>IME を無効化する（キー設定モード開始時）。</summary>
    void Suspend();

    /// <summary>既定の IME 関連付けへ戻す（キー設定モード終了時）。</summary>
    void Restore();
}

/// <summary>
/// <c>ImmAssociateContextEx</c> による実装。対象ウィンドウの HWND は
/// <see cref="IWindowHandleProvider"/> 経由で取得するため、呼び出し側は HWND を意識しない。
/// </summary>
public sealed class ImeGuard(IWindowHandleProvider windowHandle) : IImeGuard
{
    public void Suspend() =>
        PInvoke.ImmAssociateContextEx((HWND)windowHandle.Handle, default(HIMC), 0);

    public void Restore() =>
        PInvoke.ImmAssociateContextEx((HWND)windowHandle.Handle, default(HIMC), PInvoke.IACE_DEFAULT);
}
