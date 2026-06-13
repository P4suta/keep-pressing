using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeepPressing.Interop;

/// <summary>登録可能なホットキーの識別子（RegisterHotKey の id にそのまま使う）。</summary>
public enum HotkeyId
{
    Toggle = 1,
    CaptureConfirm = 2,
    CaptureCancel = 3,

    /// <summary>緊急停止（実行中のみ Esc を全体登録し、押下で必ず停止する）。</summary>
    EmergencyStop = 4,
}

/// <summary>修飾キー。値は Win32 の MOD_* と一致する。</summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// RegisterHotKey(hWnd=NULL) によるグローバルホットキー受信。
/// WM_HOTKEY は呼び出しスレッドのメッセージキューに投函されるため、
/// 専用バックグラウンドスレッド 1 本だけで完結する（ウィンドウも WNDPROC も不要）。
/// MOD_NOREPEAT を全登録に無条件付与し、押しっぱなしによるオートリピート発火を OS レベルで防ぐ。
/// </summary>
public sealed class HotkeyListener : IDisposable
{
    private const uint RequestMessage = PInvoke.WM_APP + 1;

    private readonly ConcurrentQueue<Request> _requests = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private uint _threadId;
    private bool _disposed;

    /// <summary>ホットキー押下。リスナースレッド上で発火する — 購読側が UI スレッドへマーシャリングすること。</summary>
    public event Action<HotkeyId>? Pressed;

    public HotkeyListener()
    {
        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "KeepPressing.Hotkeys",
        };
        _thread.Start();
    }

    /// <summary>ホットキーを登録する。他アプリと競合している場合は false を返す。どのスレッドからでも呼べる。</summary>
    public Task<bool> RegisterAsync(HotkeyId id, HotkeyModifiers modifiers, ushort vk) =>
        PostRequestAsync(new Request.Register(id, modifiers, vk));

    public Task UnregisterAsync(HotkeyId id) => PostRequestAsync(new Request.Unregister(id));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _ready.Task.Wait();   // スレッド起動直後に完了するため実質待たない
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        _thread.Join();
    }

    private async Task<bool> PostRequestAsync(Request request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // 初期化ハンドシェイク: キュー生成（PeekMessage）前の PostThreadMessage は失敗しうる。
        await _ready.Task;
        _requests.Enqueue(request);
        PInvoke.PostThreadMessage(_threadId, RequestMessage, default, default);
        return await request.Completion.Task;
    }

    private void RunMessageLoop()
    {
        // メッセージキューを強制生成してから ready を立てる。
        PInvoke.PeekMessage(out _, HWND.Null, PInvoke.WM_USER, PInvoke.WM_USER, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        _threadId = PInvoke.GetCurrentThreadId();
        _ready.SetResult();

        var registered = new HashSet<int>();
        while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0).Value > 0)
        {
            switch (msg.message)
            {
                case PInvoke.WM_HOTKEY:
                    Pressed?.Invoke((HotkeyId)(nint)msg.wParam.Value);
                    break;
                case RequestMessage:
                    DrainRequests(registered);
                    break;
            }
        }

        // WM_QUIT 後: UnregisterHotKey は登録と同一スレッドで呼ぶ必要があるため、ループ脱出後ここで解除する。
        foreach (var id in registered)
        {
            PInvoke.UnregisterHotKey(HWND.Null, id);
        }
    }

    private void DrainRequests(HashSet<int> registered)
    {
        while (_requests.TryDequeue(out var request))
        {
            switch (request)
            {
                case Request.Register(var id, var modifiers, var vk):
                    bool ok = PInvoke.RegisterHotKey(
                        HWND.Null,
                        (int)id,
                        (HOT_KEY_MODIFIERS)modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT,
                        vk);
                    if (ok)
                    {
                        registered.Add((int)id);
                    }

                    request.Completion.SetResult(ok);
                    break;

                case Request.Unregister(var id):
                    PInvoke.UnregisterHotKey(HWND.Null, (int)id);
                    registered.Remove((int)id);
                    request.Completion.SetResult(true);
                    break;
            }
        }
    }

    private abstract record Request
    {
        private Request() { }

        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public sealed record Register(HotkeyId Id, HotkeyModifiers Modifiers, ushort Vk) : Request;

        public sealed record Unregister(HotkeyId Id) : Request;
    }
}
