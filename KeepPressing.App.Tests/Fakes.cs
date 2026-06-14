using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KeepPressing.Core;
using KeepPressing.Interop;
using KeepPressing.Presentation;

namespace KeepPressing.App.Tests;

/// <summary>登録/解除を記録し、Pressed を任意に発火できる <see cref="IHotkeyListener"/> の Fake。</summary>
internal sealed class FakeHotkeyListener : IHotkeyListener
{
    public event Action<HotkeyId>? Pressed;

    public List<(HotkeyId Id, ushort Vk)> Registered { get; } = [];
    public List<HotkeyId> Unregistered { get; } = [];

    /// <summary>登録を拒否する VK の集合（他アプリとの競合の模擬）。</summary>
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

/// <summary>UI 搬送を同期実行する <see cref="IUiDispatcher"/> の Fake（テストでマーシャリングを単純化）。</summary>
internal sealed class SynchronousUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
}

/// <summary>カーソル位置を固定値で返す <see cref="ICursorLocator"/> の Fake。</summary>
internal sealed class FakeCursorLocator : ICursorLocator
{
    public ScreenPoint Current { get; set; }
}

/// <summary>送出を記録する <see cref="IInputSynthesizer"/> の Fake（VM が組み立てた spec の検証に使う）。</summary>
internal sealed class RecordingInputSynthesizer : IInputSynthesizer
{
    public List<InputTarget> Taps { get; } = [];
    public List<InputTarget> Presses { get; } = [];
    public List<InputTarget> Releases { get; } = [];

    public void Press(InputTarget target) => Presses.Add(target);
    public void Release(InputTarget target) => Releases.Add(target);
    public void Tap(InputTarget target) => Taps.Add(target);
}

/// <summary>キー（と引数）をそのまま返す <see cref="ILocalizer"/> の Fake（PRI 無しでテスト可能にする）。</summary>
internal sealed class FakeLocalizer : ILocalizer
{
    public string GetString(string key) => key;

    public string Format(string key, params object[] args) =>
        args.Length == 0 ? key : $"{key}({string.Join(",", args)})";
}
