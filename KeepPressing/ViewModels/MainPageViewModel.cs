using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeepPressing.Core;
using KeepPressing.Interop;
using Microsoft.UI.Dispatching;

namespace KeepPressing.ViewModels;

/// <summary>
/// メイン画面の ViewModel。
/// マーシャリング規律: Interop/Core から飛んでくるイベント（ホットキー、StateChanged、Faulted）は
/// すべてここで <see cref="DispatcherQueue.TryEnqueue(DispatcherQueueHandler)"/> により UI スレッドへ搬送する。
/// エンジンの Start/StopAsync は UI スレッドからのみ呼ぶ（PressEngine のスレッド契約）。
/// </summary>
public sealed partial class MainPageViewModel : ObservableObject
{
    private const ushort VkF8 = 0x77;
    private const ushort VkEscape = 0x1B;

    // ComboBox の並びと一致させる。F8 は座標キャプチャ確定用に予約するため除外。
    private static readonly string[] HotkeyNames = ["F5", "F6", "F7", "F9", "F10"];
    private static readonly ushort[] HotkeyVks = [0x74, 0x75, 0x76, 0x78, 0x79];

    private readonly PressEngine _engine;
    private readonly HotkeyListener _hotkeys;
    private readonly ICursorLocator _cursor;
    private readonly DispatcherQueue _dispatcher;

    private KeyCode? _capturedKey;
    private TaskCompletionSource<ScreenPoint>? _captureResult;
    private int _lastHotkeyIndex;
    private bool _revertingHotkey;

    public MainPageViewModel(PressEngine engine, HotkeyListener hotkeys, ICursorLocator cursor, DispatcherQueue dispatcher)
    {
        (_engine, _hotkeys, _cursor) = (engine, hotkeys, cursor);
        _dispatcher = dispatcher;

        KeyDisplay = "未設定";
        IntervalMs = 50;
        LivePositionText = "";
        StatusText = "停止中";

        // 初期値の代入で OnHotkeyIndexChanged → 登録 が走らないよう抑止し、登録経路を 1 本にする。
        _revertingHotkey = true;
        HotkeyIndex = _lastHotkeyIndex = 1;   // F6
        _revertingHotkey = false;

        _hotkeys.Pressed += id => _dispatcher.TryEnqueue(() => OnHotkey(id));
        _engine.StateChanged += s => _dispatcher.TryEnqueue(() => OnEngineState(s));
        _engine.Faulted += ex => _dispatcher.TryEnqueue(async () =>
        {
            await _engine.StopAsync();
            ErrorMessage = $"入力の送出中にエラーが発生したため停止しました: {ex.Message}";
        });

        _ = ChangeHotkeyAsync(HotkeyIndex);   // 初期ホットキー登録（変更時と同一経路）
    }

    // ---- 設定状態 -------------------------------------------------------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMouseTarget), nameof(IsKeyboardTarget))]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial int TargetKindIndex { get; set; }          // 0: マウス / 1: キーボード

    [ObservableProperty]
    public partial int MouseButtonIndex { get; set; }         // 0: 左 / 1: 右 / 2: 中

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
    public partial int ModeIndex { get; set; }                // 0: 連打 / 1: 長押し

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyName), nameof(ToggleButtonLabel))]
    public partial int HotkeyIndex { get; set; }

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

    public bool IsMouseTarget => TargetKindIndex == 0;
    public bool IsKeyboardTarget => TargetKindIndex == 1;
    public bool IsRepeatMode => ModeIndex == 0;
    public bool IsNotRunning => !IsRunning;
    public bool HasError => ErrorMessage is not null;
    public string HotkeyName => HotkeyNames[Math.Clamp(HotkeyIndex, 0, HotkeyNames.Length - 1)];
    public string ToggleButtonLabel => IsRunning ? $"■ 停止 ({HotkeyName})" : $"▶ 開始 ({HotkeyName})";
    public string CpsText => $"≈ 毎秒 {1000 / Math.Max(IntervalMs, 1):0} 回";

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

    /// <summary>UI 状態 → ドメイン型（ADT）への翻訳の単一点。</summary>
    private bool TryBuildSpec(out PressSpec spec, out string? error)
    {
        // NumberBox は空欄のとき NaN を返すため、ドメイン型へ翻訳する前にここで弾く。
        if (IsRepeatMode && double.IsNaN(IntervalMs))
        {
            (spec, error) = (null!, "連打間隔を入力してください。");
            return false;
        }

        InputTarget target;
        if (IsMouseTarget)
        {
            if (UseFixedPosition && (double.IsNaN(FixedX) || double.IsNaN(FixedY)))
            {
                (spec, error) = (null!, "固定座標の X / Y を入力してください。");
                return false;
            }

            var button = MouseButtonIndex switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Right,
                _ => MouseButton.Middle,
            };
            var position = UseFixedPosition ? new ScreenPoint((int)FixedX, (int)FixedY) : (ScreenPoint?)null;
            target = new InputTarget.Mouse(button, position);
        }
        else if (_capturedKey is { } key)
        {
            target = new InputTarget.Key(key);
        }
        else
        {
            (spec, error) = (null!, "連打するキーを設定してください。");
            return false;
        }

        PressMode mode = IsRepeatMode
            ? new PressMode.Repeat(TimeSpan.FromMilliseconds(Math.Max(IntervalMs, 1)))
            : PressMode.Hold.Instance;
        (spec, error) = (new PressSpec(target, mode), null);
        return true;
    }

    private void OnEngineState(EngineState state)
    {
        IsRunning = state is EngineState.Running;
        StatusText = IsRunning ? $"{Describe(state)}（Esc連打で緊急停止）" : Describe(state);

        // 緊急脱出: 実行中だけ Esc を全体登録する。連打中は対象ウィンドウにフォーカスが奪われ
        // 設定したホットキーも忘れがちなので、誰でも知っている Esc を停止専用キーにする。
        // （Esc は座標キャプチャでも使うが、実行中はキャプチャ不可なので競合しない）
        if (IsRunning)
        {
            _ = _hotkeys.RegisterAsync(HotkeyId.EmergencyStop, HotkeyModifiers.None, VkEscape);
        }
        else
        {
            _ = _hotkeys.UnregisterAsync(HotkeyId.EmergencyStop);
        }
    }

    private string Describe(EngineState state) => state switch
    {
        EngineState.Idle => "停止中",
        EngineState.Running(var spec) => $"実行中: {Describe(spec)}",
        _ => throw new UnreachableException(),
    };

    private string Describe(PressSpec spec)
    {
        var target = spec.Target switch
        {
            InputTarget.Mouse(var button, var position) =>
                button switch
                {
                    MouseButton.Left => "左クリック",
                    MouseButton.Right => "右クリック",
                    _ => "中クリック",
                }
                + (position is { } p ? $"（固定 {p.X}, {p.Y}）" : "（カーソル位置）"),
            InputTarget.Key => $"キー {KeyDisplay}",
            _ => throw new UnreachableException(),
        };
        return spec.Mode switch
        {
            PressMode.Repeat repeat => $"{target} を {repeat.Interval.TotalMilliseconds:0}ms 間隔で連打",
            PressMode.Hold => $"{target} を長押し",
            _ => throw new UnreachableException(),
        };
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

    partial void OnHotkeyIndexChanged(int value)
    {
        if (!_revertingHotkey)
        {
            _ = ChangeHotkeyAsync(value);
        }
    }

    private async Task ChangeHotkeyAsync(int newIndex)
    {
        await _hotkeys.UnregisterAsync(HotkeyId.Toggle);
        if (await _hotkeys.RegisterAsync(HotkeyId.Toggle, HotkeyModifiers.None, HotkeyVks[newIndex]))
        {
            _lastHotkeyIndex = newIndex;
            ErrorMessage = null;
            return;
        }

        // 競合: 直前のキーへ巻き戻して再登録する。
        ErrorMessage = $"{HotkeyNames[newIndex]} は他のアプリが使用中のため割り当てられませんでした。";
        _revertingHotkey = true;
        HotkeyIndex = _lastHotkeyIndex;
        _revertingHotkey = false;
        await _hotkeys.RegisterAsync(HotkeyId.Toggle, HotkeyModifiers.None, HotkeyVks[_lastHotkeyIndex]);
    }

    // ---- 座標キャプチャ ---------------------------------------------------

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task CapturePositionAsync(CancellationToken ct)
    {
        if (!await _hotkeys.RegisterAsync(HotkeyId.CaptureConfirm, HotkeyModifiers.None, VkF8))
        {
            ErrorMessage = "F8 を登録できませんでした（他のアプリが使用中）。";
            return;
        }

        if (!await _hotkeys.RegisterAsync(HotkeyId.CaptureCancel, HotkeyModifiers.None, VkEscape))
        {
            await _hotkeys.UnregisterAsync(HotkeyId.CaptureConfirm);
            ErrorMessage = "Esc を登録できませんでした（他のアプリが使用中）。";
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
