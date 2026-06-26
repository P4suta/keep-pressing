using Microsoft.UI.Xaml;

namespace KeepPressing.Interop;

/// <summary>
/// Supplies the main window's HWND. The window is created after composition, so the provider is built empty
/// and attached later via <see cref="WindowHandleProvider.Attach(Window)"/>.
/// </summary>
public interface IWindowHandleProvider
{
    /// <summary>HWND. Accessing it before attach throws <see cref="System.InvalidOperationException"/>.</summary>
    nint Handle { get; }
}
