using System.Diagnostics;
using KeepPressing.Core;

namespace KeepPressing.Presentation;

/// <summary>
/// ドメイン状態（<see cref="EngineState"/> / <see cref="PressSpec"/>）を表示文字列へ整形する純粋関数。
/// Core に表示文言を置かない方針のため UI 層に置く。キー対象の表示名は VM 由来のため引数で受ける。
/// </summary>
public static class SpecDescriber
{
    public static string Describe(EngineState state, string keyDisplay) => state switch
    {
        EngineState.Idle => "停止中",
        EngineState.Running(var spec) => $"実行中: {Describe(spec, keyDisplay)}",
        _ => throw new UnreachableException(),
    };

    public static string Describe(PressSpec spec, string keyDisplay)
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
            InputTarget.Key => $"キー {keyDisplay}",
            _ => throw new UnreachableException(),
        };
        return spec.Mode switch
        {
            PressMode.Repeat repeat => $"{target} を {repeat.Interval.TotalMilliseconds:0}ms 間隔で連打",
            PressMode.Hold => $"{target} を長押し",
            _ => throw new UnreachableException(),
        };
    }
}
