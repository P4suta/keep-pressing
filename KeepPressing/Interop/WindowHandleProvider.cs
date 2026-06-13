using System;
using Microsoft.UI.Xaml;

namespace KeepPressing.Interop;

/// <summary>
/// <see cref="IWindowHandleProvider"/> の実装。合成ルート(App)だけが <see cref="Attach(Window)"/> を呼ぶ
/// （ポートには Attach を出さず、利用側は HWND の取得だけを知る）。
/// </summary>
public sealed class WindowHandleProvider : IWindowHandleProvider
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    public nint Handle => _window is { } window
        ? WinRT.Interop.WindowNative.GetWindowHandle(window)
        : throw new InvalidOperationException("ウィンドウが未アタッチです。");
}
