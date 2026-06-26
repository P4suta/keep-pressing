namespace KeepPressing.Core;

public readonly record struct ScreenPoint(int X, int Y);

/// <summary>Opaque key id (a Win32 virtual-key code). Core carries it to <see cref="IInputSynthesizer"/> without interpreting it.</summary>
public readonly record struct KeyCode(ushort Value);

/// <summary>Virtual screen bounding all monitors. The origin can be negative.</summary>
public readonly record struct VirtualScreenRect(int Left, int Top, int Width, int Height);
