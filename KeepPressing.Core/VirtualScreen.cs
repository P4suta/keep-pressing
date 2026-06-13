namespace KeepPressing.Core;

/// <summary>
/// SendInput の絶対座標規約 (0..65535) への正規化。
/// 純粋な整数演算のみで Win32 参照を持たないため、単体テストの対象にできる。
/// </summary>
public static class VirtualScreen
{
    /// <summary>
    /// 仮想スクリーン上の座標を 0..65535 の正規化座標へ変換する。
    /// 式は <c>(value - origin) * 65536 / extent</c>（long 演算 → ゼロ方向への切り捨て → 0..65535 に clamp）。
    /// </summary>
    public static (int Nx, int Ny) Normalize(ScreenPoint p, VirtualScreenRect r)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(r.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(r.Height);
        return (NormalizeAxis(p.X, r.Left, r.Width), NormalizeAxis(p.Y, r.Top, r.Height));

        static int NormalizeAxis(int value, int origin, int extent) =>
            Math.Clamp((int)((long)(value - origin) * 65536 / extent), 0, 65535);
    }
}
