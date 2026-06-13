using Microsoft.UI.Xaml;

namespace KeepPressing.Interop;

/// <summary>
/// メインウィンドウのネイティブハンドル(HWND)を供給するポート。
/// ウィンドウは合成後に生成されるため、合成時は器だけ作り、生成後に
/// <see cref="WindowHandleProvider.Attach(Window)"/> でアタッチする。
/// </summary>
public interface IWindowHandleProvider
{
    /// <summary>HWND。アタッチ前のアクセスは <see cref="System.InvalidOperationException"/>。</summary>
    nint Handle { get; }
}
