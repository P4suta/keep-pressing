using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KeepPressing.Core;
using KeepPressing.Interop;
using KeepPressing.Presentation;

namespace KeepPressing.App.Tests;

/// <summary>Fake <see cref="IHotkeyListener"/> recording register/unregister and raising Pressed on demand.</summary>
internal sealed class FakeHotkeyListener : IHotkeyListener
{
    public event Action<HotkeyId>? Pressed;

    public List<(HotkeyId Id, ushort Vk)> Registered { get; } = [];
    public List<HotkeyId> Unregistered { get; } = [];

    /// <summary>VKs to reject registration for (simulates a conflict with another app).</summary>
    public HashSet<ushort> RejectVks { get; } = [];

    public Task<bool> RegisterAsync(HotkeyId id, HotkeyModifiers modifiers, ushort vk)
    {
        if (RejectVks.Contains(vk))
        {
            return Task.FromResult(false);
        }

        Registered.Add((id, vk));
        return Task.FromResult(true);
    }

    public Task UnregisterAsync(HotkeyId id)
    {
        Unregistered.Add(id);
        return Task.CompletedTask;
    }

    public void Raise(HotkeyId id) => Pressed?.Invoke(id);
}

/// <summary>Fake <see cref="IUiDispatcher"/> running posted work synchronously.</summary>
internal sealed class SynchronousUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>Fake <see cref="ICursorLocator"/> returning a fixed position.</summary>
internal sealed class FakeCursorLocator : ICursorLocator
{
    public ScreenPoint Current { get; set; }
}

/// <summary>Fake <see cref="IInputSynthesizer"/> recording sends, used to verify the spec the VM built.</summary>
internal sealed class RecordingInputSynthesizer : IInputSynthesizer
{
    public List<InputTarget> Taps { get; } = [];
    public List<InputTarget> Presses { get; } = [];
    public List<InputTarget> Releases { get; } = [];

    public void Press(InputTarget target) => Presses.Add(target);
    public void Release(InputTarget target) => Releases.Add(target);
    public void Tap(InputTarget target) => Taps.Add(target);
}

/// <summary>Fake <see cref="ILocalizer"/> echoing the key (and args), so tests run without PRI.</summary>
internal sealed class FakeLocalizer : ILocalizer
{
    public string GetString(string key) => key;

    public string Format(string key, params object[] args) =>
        args.Length == 0 ? key : $"{key}({string.Join(",", args)})";
}
