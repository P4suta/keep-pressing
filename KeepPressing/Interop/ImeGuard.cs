using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace KeepPressing.Interop;

/// <summary>
/// Temporarily detaches the window's IME. While capturing a key, an active IME (e.g. Japanese input) can
/// swallow character keys into composition so KeyDown never reaches the app; this prevents that.
/// </summary>
public interface IImeGuard
{
    void Suspend();

    void Restore();
}

public sealed class ImeGuard(IWindowHandleProvider windowHandle) : IImeGuard
{
    public void Suspend() =>
        PInvoke.ImmAssociateContextEx((HWND)windowHandle.Handle, default(HIMC), 0);

    public void Restore() =>
        PInvoke.ImmAssociateContextEx((HWND)windowHandle.Handle, default(HIMC), PInvoke.IACE_DEFAULT);
}
