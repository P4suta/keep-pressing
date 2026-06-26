using System.Linq;
using System.Threading.Tasks;
using KeepPressing.Core;
using KeepPressing.Interop;
using KeepPressing.ViewModels;
using Windows.System;

namespace KeepPressing.App.Tests;

public class MainPageViewModelTests
{
    private static MainPageViewModel CreateViewModel(
        out FakeHotkeyListener hotkeys,
        out FakeCursorLocator cursor,
        out RecordingInputSynthesizer synth)
    {
        hotkeys = new FakeHotkeyListener();
        cursor = new FakeCursorLocator();
        synth = new RecordingInputSynthesizer();
        var engine = new PressEngine(synth, TimeProvider.System);
        return new MainPageViewModel(engine, hotkeys, cursor, new SynchronousUiDispatcher(), new FakeLocalizer());
    }

    [Fact]
    public void Ctor_RegistersDefaultHotkey_F6()
    {
        _ = CreateViewModel(out var hotkeys, out _, out _);
        Assert.Contains(hotkeys.Registered, r => r.Id == HotkeyId.Toggle && r.Vk == (ushort)VirtualKey.F6);
    }

    [Fact]
    public void ChangeHotkey_OnConflict_RevertsToPreviousAndReportsError()
    {
        var vm = CreateViewModel(out var hotkeys, out _, out _);
        var f7 = vm.HotkeyChoices.First(h => h.Vk == VirtualKey.F7);
        hotkeys.RejectVks.Add((ushort)VirtualKey.F7);

        vm.SelectedHotkey = f7;   // Rejected -> reverts to the previous F6.

        Assert.Equal(VirtualKey.F6, vm.SelectedHotkey.Vk);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public void ChangeHotkey_OnSuccess_UpdatesSelectionAndClearsError()
    {
        var vm = CreateViewModel(out var hotkeys, out _, out _);
        var f9 = vm.HotkeyChoices.First(h => h.Vk == VirtualKey.F9);

        vm.SelectedHotkey = f9;

        Assert.Equal(VirtualKey.F9, vm.SelectedHotkey.Vk);
        Assert.Null(vm.ErrorMessage);
        Assert.Contains(hotkeys.Registered, r => r.Id == HotkeyId.Toggle && r.Vk == (ushort)VirtualKey.F9);
    }

    [Fact]
    public void CanToggle_MouseTarget_IsTrue()
    {
        var vm = CreateViewModel(out _, out _, out _);
        vm.SelectedTarget = TargetKind.Mouse;
        Assert.True(vm.ToggleCommand.CanExecute(null));
    }

    [Fact]
    public void CanToggle_KeyboardWithoutKey_IsFalse()
    {
        var vm = CreateViewModel(out _, out _, out _);
        vm.SelectedTarget = TargetKind.Keyboard;
        Assert.False(vm.ToggleCommand.CanExecute(null));
    }

    [Fact]
    public void CanToggle_KeyboardAfterKeyCaptured_IsTrue()
    {
        var vm = CreateViewModel(out _, out _, out _);
        vm.SelectedTarget = TargetKind.Keyboard;
        vm.OnKeyCaptured((ushort)VirtualKey.A, "A");
        Assert.True(vm.ToggleCommand.CanExecute(null));
    }

    [Fact]
    public async Task CapturePosition_OnConfirm_SetsFixedPositionFromCursor()
    {
        var vm = CreateViewModel(out var hotkeys, out var cursor, out _);
        cursor.Current = new ScreenPoint(123, 456);

        var capture = vm.CapturePositionCommand.ExecuteAsync(null);
        Assert.True(vm.IsCapturingPosition);   // After F8/Esc registration, awaiting confirm.
        hotkeys.Raise(HotkeyId.CaptureConfirm);
        await capture;

        Assert.Equal(123, vm.FixedX);
        Assert.Equal(456, vm.FixedY);
        Assert.True(vm.UseFixedPosition);
        Assert.False(vm.IsCapturingPosition);
        Assert.Contains(HotkeyId.CaptureConfirm, hotkeys.Unregistered);   // Unregistered in finally.
        Assert.Contains(HotkeyId.CaptureCancel, hotkeys.Unregistered);
    }

    [Fact]
    public async Task CapturePosition_OnCancel_LeavesPositionUnchanged()
    {
        var vm = CreateViewModel(out var hotkeys, out var cursor, out _);
        cursor.Current = new ScreenPoint(50, 60);

        var capture = vm.CapturePositionCommand.ExecuteAsync(null);
        hotkeys.Raise(HotkeyId.CaptureCancel);
        await capture;

        Assert.Equal(0, vm.FixedX);   // Unchanged from default.
        Assert.Equal(0, vm.FixedY);
        Assert.False(vm.IsCapturingPosition);
    }

    [Fact]
    public async Task Toggle_MouseTarget_StartsEngineWithMouseSpecAndRegistersEmergencyStop()
    {
        var vm = CreateViewModel(out var hotkeys, out _, out var synth);
        vm.SelectedTarget = TargetKind.Mouse;

        await vm.ToggleCommand.ExecuteAsync(null);   // Start
        Assert.True(vm.IsRunning);
        Assert.Contains(hotkeys.Registered, r => r.Id == HotkeyId.EmergencyStop);
        Assert.Contains(synth.Taps, t => t is InputTarget.Mouse m && m.Button == MouseButton.Left);

        await vm.ToggleCommand.ExecuteAsync(null);   // Stop
        Assert.False(vm.IsRunning);
        Assert.Contains(HotkeyId.EmergencyStop, hotkeys.Unregistered);
    }
}
