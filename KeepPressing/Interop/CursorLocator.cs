using KeepPressing.Core;
using Windows.Win32;

namespace KeepPressing.Interop;

/// <summary>Current cursor position.</summary>
public interface ICursorLocator
{
    ScreenPoint Current { get; }
}

public sealed class CursorLocator : ICursorLocator
{
    public ScreenPoint Current
    {
        get
        {
            PInvoke.GetCursorPos(out var point);
            return new ScreenPoint(point.X, point.Y);
        }
    }
}
