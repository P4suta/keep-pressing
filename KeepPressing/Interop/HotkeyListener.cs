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

/// <summary>Hotkey id, used directly as the RegisterHotKey id.</summary>
public enum HotkeyId
{
    Toggle = 1,
    CaptureConfirm = 2,
    CaptureCancel = 3,

    /// <summary>Emergency stop: Esc is registered globally only while running and always stops.</summary>
    EmergencyStop = 4,
}

/// <summary>Modifier keys. Values match Win32 MOD_*.</summary>
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
/// Global hotkeys via RegisterHotKey(hWnd=NULL). WM_HOTKEY is posted to the calling thread's message
/// queue, so a single dedicated background thread suffices (no window or WNDPROC). MOD_NOREPEAT is added
/// to every registration to suppress auto-repeat from a held key at the OS level.
/// </summary>
public sealed class HotkeyListener : IHotkeyListener, IDisposable
{
    private const uint RequestMessage = PInvoke.WM_APP + 1;

    private readonly ConcurrentQueue<Request> _requests = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private uint _threadId;
    private bool _disposed;

    /// <summary>Hotkey pressed. Fires on the listener thread — subscribers must marshal to the UI thread.</summary>
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

    /// <summary>Registers a hotkey. Returns false if it conflicts with another app. Callable from any thread.</summary>
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
        _ready.Task.Wait();   // Completes right after thread start, so effectively no wait.
        PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
        _thread.Join();
    }

    private async Task<bool> PostRequestAsync(Request request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Init handshake: PostThreadMessage can fail before the queue is created (PeekMessage).
        await _ready.Task;
        _requests.Enqueue(request);
        PInvoke.PostThreadMessage(_threadId, RequestMessage, default, default);
        return await request.Completion.Task;
    }

    private void RunMessageLoop()
    {
        // Force-create the message queue before signaling ready.
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

        // After WM_QUIT: UnregisterHotKey must run on the registering thread, so unregister here.
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
