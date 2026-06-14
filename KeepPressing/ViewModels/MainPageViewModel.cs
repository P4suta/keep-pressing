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
/// メイン画面の ViewModel。
/// マーシャリング規律: Interop/Core から飛んでくるイベント（ホットキー、StateChanged、Faulted）は
/// すべてここで <see cref="IUiDispatcher.Post(System.Action)"/> により UI スレッドへ搬送する。
/// エンジンの Start/StopAsync は UI スレッドからのみ呼ぶ（PressEngine のスレッド契約）。
/// </summary>
public sealed partial class MainPageViewModel : ObservableObject
{
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

        // 選択肢の表示名は言語依存のため、初期化子ではなく ctor で loc から構築する
        // （HotkeyChoices は "F5" 等の言語非依存記号なので初期化子のまま）。
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
        IntervalMs = 50;
        LivePositionText = "";
        StatusText = loc.GetString("Status_Idle");

        // 初期選択を _lastHotkey と一致させることで、setter が走らせる OnSelectedHotkeyChanged を no-op にし、
        // 登録経路を下の明示呼び出し 1 本に統一する（値ベースの再入制御。フラグを持たない）。
        SelectedHotkey = _lastHotkey = HotkeyChoices[1];   // F6

        _hotkeys.Pressed += id => _dispatcher.Post(() => OnHotkey(id));
        _engine.StateChanged += s => _dispatcher.Post(() => OnEngineState(s));
        _engine.Faulted += ex => _dispatcher.Post(async () =>
        {
            await _engine.StopAsync();
            ErrorMessage = _loc.Format("Error_Faulted", ex.Message);
        });

        _ = ChangeHotkeyAsync(SelectedHotkey);   // 初期ホットキー登録（変更時と同一経路）
    }

    // ---- 選択肢（単一の真実の源。XAML は ItemsSource でバインドし、項目の並びを XAML に重複させない）----

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

    // ---- 設定状態 -------------------------------------------------------

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

    // ---- 実行状態 -------------------------------------------------------

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

    // ---- 導出プロパティ -------------------------------------------------

    public bool IsMouseTarget => SelectedTarget is TargetKind.Mouse;
    public bool IsKeyboardTarget => SelectedTarget is TargetKind.Keyboard;
    public bool IsRepeatMode => SelectedMode.Value is PressModeKind.Repeat;
    public bool IsNotRunning => !IsRunning;
    public bool HasError => ErrorMessage is not null;
    public string ToggleButtonLabel => IsRunning
        ? _loc.Format("Toggle_Stop", SelectedHotkey.DisplayKey)
        : _loc.Format("Toggle_Start", SelectedHotkey.DisplayKey);
    public string CpsText => _loc.Format("Cps_Format", (long)Math.Round(1000 / Math.Max(IntervalMs, 1)));

    // ---- 開始 / 停止 ----------------------------------------------------

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

    /// <summary>UI 状態 → ドメイン型（ADT）への翻訳。純粋関数 <see cref="SpecBuilder"/> へ委譲する。</summary>
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

        // 緊急脱出: 実行中だけ Esc を全体登録する。連打中は対象ウィンドウにフォーカスが奪われ
        // 設定したホットキーも忘れがちなので、誰でも知っている Esc を停止専用キーにする。
        // （Esc は座標キャプチャでも使うが、実行中はキャプチャ不可なので競合しない）
        if (IsRunning)
        {
            _ = _hotkeys.RegisterAsync(HotkeyId.EmergencyStop, HotkeyModifiers.None, (ushort)VirtualKey.Escape);
        }
        else
        {
            _ = _hotkeys.UnregisterAsync(HotkeyId.EmergencyStop);
        }
    }

    // ---- グローバルホットキー --------------------------------------------

    private void OnHotkey(HotkeyId id)
    {
        switch (id)
        {
            case HotkeyId.Toggle when ToggleCommand.CanExecute(null):
                ToggleCommand.Execute(null);
                break;
            case HotkeyId.CaptureConfirm:
                // F8 押下「時点」のカーソル位置を確定値にする（50ms ポーリングの遅延に影響されない）。
                _captureResult?.TrySetResult(_cursor.Current);
                break;
            case HotkeyId.CaptureCancel:
                if (CapturePositionCancelCommand.CanExecute(null))
                {
                    CapturePositionCancelCommand.Execute(null);
                }

                break;

            // 緊急停止は「止める」だけ。実行中以外では何もしない（登録解除との競合で誤って開始しないよう守る）。
            case HotkeyId.EmergencyStop when _engine.State is EngineState.Running && ToggleCommand.CanExecute(null):
                ToggleCommand.Execute(null);
                break;
        }
    }

    partial void OnSelectedHotkeyChanged(HotkeyChoice value)
    {
        // 巻き戻し代入（value == _lastHotkey）は登録を起こさない。再入を値で判定するためフラグが不要になる。
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

        // 競合: 直前のキーへ巻き戻して再登録する。
        ErrorMessage = _loc.Format("Error_HotkeyConflict", choice.DisplayKey);
        SelectedHotkey = _lastHotkey;
        await _hotkeys.RegisterAsync(HotkeyId.Toggle, HotkeyModifiers.None, (ushort)_lastHotkey.Vk);
    }

    // ---- 座標キャプチャ ---------------------------------------------------

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
        IsCapturingPosition = true;   // ← view がこれを合図に描画フレームごとの RefreshLivePosition 呼び出しを開始する
        ErrorMessage = null;
        try
        {
            var captured = await _captureResult.Task.WaitAsync(ct);
            (FixedX, FixedY) = (captured.X, captured.Y);
            UseFixedPosition = true;
        }
        catch (OperationCanceledException)
        {
            // Esc またはキャンセルボタンによる中断。
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
    /// ライブ座標表示の更新。キャプチャ中、view が描画フレームごと（CompositionTarget.Rendering）に呼ぶ。
    /// タイマーポーリング（分解能 ~16ms に丸められ表示がカクつく）ではなくフレーム同期にすることで滑らかに追従する。
    /// </summary>
    public void RefreshLivePosition()
    {
        var p = _cursor.Current;
        LivePositionText = $"({p.X}, {p.Y})";   // 同値なら SetProperty が通知を抑制する
    }

    // ---- キー設定 ---------------------------------------------------------

    /// <summary>MainPage の PreviewKeyDown から呼ばれる（キー設定モード中のみ）。</summary>
    public void OnKeyCaptured(ushort vk, string displayName)
    {
        _capturedKey = new KeyCode(vk);
        KeyDisplay = displayName;
        IsKeyCaptureArmed = false;
        ToggleCommand.NotifyCanExecuteChanged();
    }

    public void ClearError() => ErrorMessage = null;
}
