using System;
using System.Threading.Tasks;

namespace KeepPressing.Interop;

/// <summary>Port for registering and receiving global hotkeys, so tests can swap in a fake.</summary>
public interface IHotkeyListener
{
    /// <summary>Hotkey pressed. Fires on the listener thread — subscribers must marshal to the UI thread.</summary>
    event Action<HotkeyId>? Pressed;

    /// <summary>Registers a hotkey. Returns false if it conflicts with another app. Callable from any thread.</summary>
    Task<bool> RegisterAsync(HotkeyId id, HotkeyModifiers modifiers, ushort vk);

    Task UnregisterAsync(HotkeyId id);
}
