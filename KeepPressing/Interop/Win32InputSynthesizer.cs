using System;
using System.Diagnostics;
using KeepPressing.Core;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace KeepPressing.Interop;

/// <summary>
/// SendInput による <see cref="IInputSynthesizer"/> 実装。ステートレスでスレッドセーフ。
/// </summary>
public sealed class Win32InputSynthesizer : IInputSynthesizer
{
    public void Press(InputTarget target) => Send([Build(target, down: true)]);

    public void Release(InputTarget target) => Send([Build(target, down: false)]);

    /// <summary>Down→Up を 1 回の SendInput で送出する。呼び出し内のイベント列には他入力が割り込まない。</summary>
    public void Tap(InputTarget target) => Send([Build(target, down: true), Build(target, down: false)]);

    private static unsafe void Send(ReadOnlySpan<INPUT> inputs)
    {
        var sent = PInvoke.SendInput(inputs, sizeof(INPUT));
        if (sent != inputs.Length)
        {
            // UIPI（昇格ウィンドウへの遮断）は無音で起きるため、例外でループを殺さず記録に留める。
            Debug.WriteLine($"SendInput: {inputs.Length} 件中 {sent} 件しか送出されなかった。");
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
            // Down/Up 双方に move を合成する: Hold 中に物理マウスが動いても Up は固定点で発生し、
            // 意図しないドラッグにならない。メトリクスは毎回取得してモニタ構成の変化に追従する。
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
        // スキャンコード主体: Raw Input / DirectInput 系は VK のみの合成入力を取りこぼすことがあり、
        // 通常アプリはスキャン→VK 変換されるため両対応できる。
        var scan = PInvoke.MapVirtualKey(vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
        var (wVk, wScan, flags) = scan != 0
            ? ((VIRTUAL_KEY)0,
               (ushort)(scan & 0xFF),
               KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE
                   | ((scan & 0xFF00) != 0 ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0))
            : ((VIRTUAL_KEY)vk, (ushort)0, default(KEYBD_EVENT_FLAGS));   // スキャンコードを持たないキーは VK へフォールバック

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
