namespace KeepPressing.Core;

/// <summary>スクリーン座標（仮想スクリーン座標系の物理ピクセル）。</summary>
public readonly record struct ScreenPoint(int X, int Y);

/// <summary>
/// キーを識別する不透明値（実体は Win32 仮想キーコード）。
/// Core はこの値を解釈せず、そのまま <see cref="IInputSynthesizer"/> へ運ぶ。
/// </summary>
public readonly record struct KeyCode(ushort Value);

/// <summary>仮想スクリーン（全モニタを包含する矩形）。原点は負になりうる。</summary>
public readonly record struct VirtualScreenRect(int Left, int Top, int Width, int Height);
