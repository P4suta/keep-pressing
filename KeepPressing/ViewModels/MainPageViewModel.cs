using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepPressing.Core;
using KeepPressing.Interop;
using KeepPressing.Presentation;
using VirtualKey = Windows.System.VirtualKey;

namespace KeepPressing.ViewModels;

/// <summary>
/// Main page ViewModel. Marshaling rule: every event from Interop/Core (hotkeys, StateChanged, Faulted) is
/// posted to the UI thread here via <see cref="IUiDispatcher.Post(System.Action)"/>. The engine's
/// Start/StopAsync are called only from the UI thread (PressEngine's threading contract).
/// </summary>
public sealed partial class MainPageViewModel : ObservableObject
{
    // Interval input bounds (ms): constraints for the NumberBox. The domain separately clamps to >= 1ms
    // (see SpecBuilder), so these are purely the editing range/step the UI offers.
    public const double IntervalMinMs = 10;
    public const double IntervalMaxMs = 10000;
    public const double IntervalStepMs = 10;
    public const double IntervalDefaultMs = 50;

    private readonly PressEngine _engine;
    private readonly IHotkeyListener _hotkeys;
    private readonly ICursorLocator _cursor;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILocalizer _loc;

    private KeyCode? _capturedKey;
    private TaskCompletionSource<ScreenPoint>? _captureResult;
    private HotkeyChoice _lastHotkey;

    public MainPageViewModel(PressEngine engine, IHotkeyListener hotkeys, ICursorLocator cursor, IUiDispatcher dispatcher, ILocalizer loc)
    {
        (_engine, _hotkeys, _cursor) = (engine, hotkeys, cursor);
        _dispatcher = dispatcher;
        _loc = loc;

        // Choice display names are language-dependent, so build them from loc in the ctor rather than an
        // initializer (HotkeyChoices stays an initializer — "F5" etc. are language-independent symbols).
        MouseButtons =
        [
            new(MouseButton.Left, loc.GetString("Mouse_Left")),
            new(MouseButton.Right, loc.GetString("Mouse_Right")),
            new(MouseButton.Middle, loc.GetString("Mouse_Middle")),
        ];
        Modes =
        [
            new(PressModeKind.Repeat, loc.GetString("Mode_Repeat")),
            new(PressModeKind.Hold, loc.GetString("Mode_Hold")),
        ];

        SelectedMouseButton = MouseButtons[0];
        SelectedMode = Modes[0];
        KeyDisplay = loc.GetString("Key_Unset");
        IntervalMs = IntervalDefaultMs;
        LivePositionText = "";
        StatusText = loc.GetString("Status_Idle");

        // Matching the initial selection to _lastHotkey makes the setter's OnSelectedHotkeyChanged a no-op,
        // funneling registration through the single explicit call below (value-based reentrancy, no flag).
        SelectedHotkey = _lastHotkey = HotkeyChoices[1];   // F6

        _hotkeys.Pressed += id => _dispatcher.Post(() => OnHotkey(id));
        _engine.StateChanged += s => _dispatcher.Post(() => OnEngineState(s));
        _engine.Faulted += ex => _dispatcher.Post(async () =>
        {
            await _engine.StopAsync();
            ErrorMessage = _loc.Format("Error_Faulted", ex.Message);
        });

        _ = ChangeHotkeyAsync(SelectedHotkey);   // Initial hotkey registration (same path as on change).
    }

    // ---- Choices (single source of truth; XAML binds via ItemsSource) ----

    public IReadOnlyList<Choice<MouseButton>> MouseButtons { get; }

    public IReadOnlyList<Choice<PressModeKind>> Modes { get; }

    public IReadOnlyList<HotkeyChoice> HotkeyChoices { get; } =
    [
        new(VirtualKey.F5, "F5"),
        new(VirtualKey.F6, "F6"),
        new(VirtualKey.F7, "F7"),
        new(VirtualKey.F9, "F9"),
        new(VirtualKey.F10, "F10"),
    ];

    // ---- Configuration state --------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMouseTarget), nameof(IsKeyboardTarget))]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial TargetKind SelectedTarget { get; set; }

    [ObservableProperty]
    public partial Choice<MouseButton> SelectedMouseButton { get; set; }

    [ObservableProperty]
    public partial bool UseFixedPosition { get; set; }

    [ObservableProperty]
    public partial double FixedX { get; set; }

    [ObservableProperty]
    public partial double FixedY { get; set; }

    [ObservableProperty]
    public partial string KeyDisplay { get; set; }

    [ObservableProperty]
    public partial bool IsKeyCaptureArmed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CpsText))]
    public partial double IntervalMs { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRepeatMode))]
    public partial Choice<PressModeKind> SelectedMode { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleButtonLabel))]
    public partial HotkeyChoice SelectedHotkey { get; set; }

    // ---- Run state ------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRunning), nameof(ToggleButtonLabel))]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool IsCapturingPosition { get; set; }

    [ObservableProperty]
    public partial string LivePositionText { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    // ---- Derived properties ---------------------------------------------

    public bool IsMouseTarget => SelectedTarget is TargetKind.Mouse;
    public bool IsKeyboardTarget => SelectedTarget is TargetKind.Keyboard;
    public bool IsRepeatMode => SelectedMode.Value is PressModeKind.Repeat;
    public bool IsNotRunning => !IsRunning;
    public bool HasError => ErrorMessage is not null;
    public string ToggleButtonLabel => IsRunning
        ? _loc.Format("Toggle_Stop", SelectedHotkey.DisplayKey)
        : _loc.Format("Toggle_Start", SelectedHotkey.DisplayKey);
    public string CpsText => _loc.Format("Cps_Format", (long)Math.Round(1000 / Math.Max(IntervalMs, 1)));

    // ---- Start / stop ---------------------------------------------------

    private bool CanToggle => !IsCapturingPosition && (IsRunning || IsMouseTarget || _capturedKey is not null);

    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync()
    {
        if (_engine.State is EngineState.Running)
        {
            await _engine.StopAsync();
            return;
        }

        if (!TryBuildSpec(out var spec, out var error))
        {
            ErrorMessage = error;
            return;
        }

        ErrorMessage = null;
        _engine.Start(spec);
    }

    /// <summary>Translates UI state to the domain type via the pure <see cref="SpecBuilder"/>.</summary>
    private bool TryBuildSpec(out PressSpec spec, out string? error)
    {
        var config = new PressConfiguration(
            SelectedTarget,
            SelectedMouseButton.Value,
            UseFixedPosition,
            FixedX,
            FixedY,
            _capturedKey,
            SelectedMode.Value,
            IntervalMs);
        return SpecBuilder.TryBuild(config, _loc, out spec, out error);
    }

    private void OnEngineState(EngineState state)
    {
        IsRunning = state is EngineState.Running;
        var describe = SpecDescriber.Describe(state, KeyDisplay, _loc);
        StatusText = IsRunning ? _loc.Format("Status_EmergencyHint", describe) : describe;

        // Emergency exit: register Esc globally only while running. During a run the target window holds
        // focus and the configured hotkey is easy to forget, so the universally known Esc becomes a
        // stop-only key. (Esc is also used for position capture, but capture is impossible while running.)
        if (IsRunning)
        {
            _ = _hotkeys.RegisterAsync(HotkeyId.EmergencyStop, HotkeyModifiers.None, (ushort)VirtualKey.Escape);
        }
        else
        {
            _ = _hotkeys.UnregisterAsync(HotkeyId.EmergencyStop);
        }
    }

    // ---- Global hotkeys -------------------------------------------------

    private void OnHotkey(HotkeyId id)
    {
        switch (id)
        {
            case HotkeyId.Toggle when ToggleCommand.CanExecute(null):
                ToggleCommand.Execute(null);
                break;
            case HotkeyId.CaptureConfirm:
                // Use the cursor position at the moment F8 is pressed (unaffected by the 50ms polling lag).
                _captureResult?.TrySetResult(_cursor.Current);
                break;
            case HotkeyId.CaptureCancel:
                if (CapturePositionCancelCommand.CanExecute(null))
                {
                    CapturePositionCancelCommand.Execute(null);
                }

                break;

            // Emergency stop only stops; it does nothing unless running (guards against a race with
            // unregistration accidentally starting).
            case HotkeyId.EmergencyStop when _engine.State is EngineState.Running && ToggleCommand.CanExecute(null):
                ToggleCommand.Execute(null);
                break;
        }
    }

    partial void OnSelectedHotkeyChanged(HotkeyChoice value)
    {
        // A revert assignment (value == _lastHotkey) triggers no registration; value-based reentrancy needs no flag.
        if (value != _lastHotkey)
        {
            _ = ChangeHotkeyAsync(value);
        }
    }

    private async Task ChangeHotkeyAsync(HotkeyChoice choice)
    {
        await _hotkeys.UnregisterAsync(HotkeyId.Toggle);
        if (await _hotkeys.RegisterAsync(HotkeyId.Toggle, HotkeyModifiers.None, (ushort)choice.Vk))
        {
            _lastHotkey = choice;
            ErrorMessage = null;
            return;
        }

        // Conflict: revert to the previous key and re-register it.
        ErrorMessage = _loc.Format("Error_HotkeyConflict", choice.DisplayKey);
        SelectedHotkey = _lastHotkey;
        await _hotkeys.RegisterAsync(HotkeyId.Toggle, HotkeyModifiers.None, (ushort)_lastHotkey.Vk);
    }

    // ---- Position capture -----------------------------------------------

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CapturePositionAsync(CancellationToken ct)
    {
        if (!await _hotkeys.RegisterAsync(HotkeyId.CaptureConfirm, HotkeyModifiers.None, (ushort)VirtualKey.F8))
        {
            ErrorMessage = _loc.Format("Error_RegisterFailed", "F8");
            return;
        }

        if (!await _hotkeys.RegisterAsync(HotkeyId.CaptureCancel, HotkeyModifiers.None, (ushort)VirtualKey.Escape))
        {
            await _hotkeys.UnregisterAsync(HotkeyId.CaptureConfirm);
            ErrorMessage = _loc.Format("Error_RegisterFailed", "Esc");
            return;
        }

        _captureResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RefreshLivePosition();
        IsCapturingPosition = true;   // Signals the view to start per-frame RefreshLivePosition calls.
        ErrorMessage = null;
        try
        {
            var captured = await _captureResult.Task.WaitAsync(ct);
            (FixedX, FixedY) = (captured.X, captured.Y);
            UseFixedPosition = true;
        }
        catch (OperationCanceledException)
        {
            // Aborted by Esc or the cancel button.
        }
        finally
        {
            IsCapturingPosition = false;
            _captureResult = null;
            await _hotkeys.UnregisterAsync(HotkeyId.CaptureConfirm);
            await _hotkeys.UnregisterAsync(HotkeyId.CaptureCancel);
        }
    }

    /// <summary>
    /// Updates the live coordinate display. During capture the view calls this every render frame
    /// (CompositionTarget.Rendering); frame sync tracks the cursor more smoothly than timer polling.
    /// </summary>
    public void RefreshLivePosition()
    {
        var p = _cursor.Current;
        LivePositionText = $"({p.X}, {p.Y})";   // SetProperty suppresses the notification when unchanged.
    }

    // ---- Key capture ----------------------------------------------------

    /// <summary>Called from MainPage's PreviewKeyDown (only during key-capture mode).</summary>
    public void OnKeyCaptured(ushort vk, string displayName)
    {
        _capturedKey = new KeyCode(vk);
        KeyDisplay = displayName;
        IsKeyCaptureArmed = false;
        ToggleCommand.NotifyCanExecuteChanged();
    }

    public void ClearError() => ErrorMessage = null;
}
