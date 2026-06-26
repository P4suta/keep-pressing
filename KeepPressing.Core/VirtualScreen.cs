namespace KeepPressing.Core;

/// <summary>Normalizes screen coordinates to SendInput's absolute 0..65535 range.</summary>
public static class VirtualScreen
{
    public static (int Nx, int Ny) Normalize(ScreenPoint p, VirtualScreenRect r)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(r.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(r.Height);
        return (NormalizeAxis(p.X, r.Left, r.Width), NormalizeAxis(p.Y, r.Top, r.Height));

        // (value - origin) * 65536 / extent in long math, truncated toward zero, clamped to [0, 65535].
        static int NormalizeAxis(int value, int origin, int extent) =>
            Math.Clamp((int)((long)(value - origin) * 65536 / extent), 0, 65535);
    }
}
