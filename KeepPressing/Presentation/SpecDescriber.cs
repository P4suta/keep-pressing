using System.Diagnostics;
using KeepPressing.Core;

namespace KeepPressing.Presentation;

/// <summary>
/// ドメイン状態（<see cref="EngineState"/> / <see cref="PressSpec"/>）を表示文字列へ整形する純粋関数。
/// 文言は <see cref="ILocalizer"/> 経由で受け取り、Core に表示文言を置かずテスト可能性も保つ。
/// 語順が言語で異なるため、対象部分（クリック種別＋位置）を組み立ててから動作の書式に差し込む。
/// キー対象の表示名は VM 由来のため引数で受ける。
/// </summary>
public static class SpecDescriber
{
    public static string Describe(EngineState state, string keyDisplay, ILocalizer loc) => state switch
    {
        EngineState.Idle => loc.GetString("Status_Idle"),
        EngineState.Running(var spec) => loc.Format("Status_Running", Describe(spec, keyDisplay, loc)),
        _ => throw new UnreachableException(),
    };

    public static string Describe(PressSpec spec, string keyDisplay, ILocalizer loc)
    {
        var target = spec.Target switch
        {
            InputTarget.Mouse(var button, var position) =>
                (button switch
                {
                    MouseButton.Left => loc.GetString("Mouse_Left"),
                    MouseButton.Right => loc.GetString("Mouse_Right"),
                    _ => loc.GetString("Mouse_Middle"),
                })
                + (position is { } p ? loc.Format("Target_FixedPos", p.X, p.Y) : loc.GetString("Target_CursorPos")),
            InputTarget.Key => loc.Format("Target_Key", keyDisplay),
            _ => throw new UnreachableException(),
        };
        return spec.Mode switch
        {
            PressMode.Repeat repeat => loc.Format("Mode_RepeatDesc", target, (long)repeat.Interval.TotalMilliseconds),
            PressMode.Hold => loc.Format("Mode_HoldDesc", target),
            _ => throw new UnreachableException(),
        };
    }
}
