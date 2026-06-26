using System;
using System.Diagnostics;
using KeepPressing.Core;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeepPressing.Interop;

/// <summary>SendInput-based <see cref="IInputSynthesizer"/>. Stateless and thread-safe.</summary>
public sealed class Win32InputSynthesizer : IInputSynthesizer
{
    public void Press(InputTarget target) => Send([Build(target, down: true)]);

    public void Release(InputTarget target) => Send([Build(target, down: false)]);

    /// <summary>Sends Down then Up in one SendInput call so no other input interleaves between them.</summary>
    public void Tap(InputTarget target) => Send([Build(target, down: true), Build(target, down: false)]);

    private static unsafe void Send(ReadOnlySpan<INPUT> inputs)
    {
        var sent = PInvoke.SendInput(inputs, sizeof(INPUT));
        if (sent != inputs.Length)
        {
            // UIPI blocks to elevated windows fail silently, so log instead of throwing and killing the loop.
            Debug.WriteLine($"SendInput sent {sent} of {inputs.Length} inputs.");
        }
    }

    private static INPUT Build(InputTarget target, bool down) => target switch
    {
        InputTarget.Mouse mouse => BuildMouse(mouse, down),
        InputTarget.Key key => BuildKey(key.Code.Value, down),
        _ => throw new UnreachableException(),
    };

    private static INPUT BuildMouse(InputTarget.Mouse mouse, bool down)
    {
        var flags = (mouse.Button, down) switch
        {
            (MouseButton.Left, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
            (MouseButton.Left, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
            (MouseButton.Right, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
            (MouseButton.Right, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
            (MouseButton.Middle, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN,
            (MouseButton.Middle, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP,
            _ => throw new UnreachableException(),
        };

        var (dx, dy) = (0, 0);
        if (mouse.Position is { } position)
        {
            // Fold the move into both Down and Up so that even if the physical mouse moves during a hold,
            // Up still lands at the fixed point and never drags. Metrics are read each time to track monitor changes.
            (dx, dy) = VirtualScreen.Normalize(position, GetVirtualScreenRect());
            flags |= MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE
                   | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE
                   | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;
        }

        return new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
            Anonymous =
            {
                mi = new MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = flags,
                    dwExtraInfo = (nuint)PInvoke.GetMessageExtraInfo().Value,
                },
            },
        };
    }

    private static INPUT BuildKey(ushort vk, bool down)
    {
        // Prefer scan codes: Raw Input / DirectInput can drop VK-only synthetic input, while normal apps
        // map scan codes back to VK, so this covers both.
        var scan = PInvoke.MapVirtualKey(vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
        var (wVk, wScan, flags) = scan != 0
            ? ((VIRTUAL_KEY)0,
               (ushort)(scan & 0xFF),
               KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE
                   | ((scan & 0xFF00) != 0 ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0))
            : ((VIRTUAL_KEY)vk, (ushort)0, default(KEYBD_EVENT_FLAGS));   // Keys with no scan code fall back to VK.

        if (!down)
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        }

        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous =
            {
                ki = new KEYBDINPUT
                {
                    wVk = wVk,
                    wScan = wScan,
                    dwFlags = flags,
                    dwExtraInfo = (nuint)PInvoke.GetMessageExtraInfo().Value,
                },
            },
        };
    }

    private static VirtualScreenRect GetVirtualScreenRect() => new(
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
        PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
}
