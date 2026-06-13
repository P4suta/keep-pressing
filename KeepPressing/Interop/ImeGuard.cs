using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace KeepPressing.Interop;

/// <summary>
/// ウィンドウの IME 関連付けの一時無効化。
/// キー設定モード中に IME（日本語入力など）が文字キーを composition として
/// 取り込んでしまい、KeyDown がアプリへ届かない問題を防ぐ。
/// </summary>
public static class ImeGuard
{
    /// <summary>IME を無効化する（キー設定モード開始時）。</summary>
    public static void Suspend(nint hwnd) =>
        PInvoke.ImmAssociateContextEx((HWND)hwnd, default(HIMC), 0);

    /// <summary>既定の IME 関連付けへ戻す（キー設定モード終了時）。</summary>
    public static void Restore(nint hwnd) =>
        PInvoke.ImmAssociateContextEx((HWND)hwnd, default(HIMC), PInvoke.IACE_DEFAULT);
}
