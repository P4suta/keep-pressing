using KeepPressing.Core;
using Windows.Win32;

namespace KeepPressing.Interop;

/// <summary>現在のカーソル位置の取得。</summary>
public interface ICursorLocator
{
    ScreenPoint Current { get; }
}

/// <summary>GetCursorPos による実装。</summary>
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
