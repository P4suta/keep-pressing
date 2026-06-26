using Windows.System;

namespace KeepPressing.ViewModels;

/// <summary>
/// Target kind. Kept separate from Core's <c>InputTarget</c> because the UI must allow selecting the
/// "Keyboard" tab before a key is assigned, which no finished ADT instance can represent.
/// </summary>
public enum TargetKind
{
    Mouse,
    Keyboard,
}

/// <summary>Press mode kind; translated to the <c>PressMode</c> ADT on commit.</summary>
public enum PressModeKind
{
    Repeat,
    Hold,
}

/// <summary>A choice: value plus display name. XAML binds the list via <c>ItemsSource</c> so item order is not duplicated in XAML.</summary>
public sealed record Choice<T>(T Value, string DisplayName);

/// <summary>A start/stop hotkey choice: VK plus a language-independent display symbol ("F5", ...).</summary>
public sealed record HotkeyChoice(VirtualKey Vk, string DisplayKey);
