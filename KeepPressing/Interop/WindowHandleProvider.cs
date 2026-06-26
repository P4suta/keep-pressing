using System;
using Microsoft.UI.Xaml;

namespace KeepPressing.Interop;

/// <summary>Only the composition root (App) calls <see cref="Attach(Window)"/>; consumers see HWND access only.</summary>
public sealed class WindowHandleProvider : IWindowHandleProvider
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    public nint Handle => _window is { } window
        ? WinRT.Interop.WindowNative.GetWindowHandle(window)
        : throw new InvalidOperationException("Window is not attached.");
}
