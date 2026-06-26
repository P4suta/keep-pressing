using System.Diagnostics;
using KeepPressing.Core;

namespace KeepPressing.Presentation;

/// <summary>
/// Pure function formatting domain state (<see cref="EngineState"/> / <see cref="PressSpec"/>) into display
/// strings via <see cref="ILocalizer"/>. The target part (button + position) is built first, then inserted
/// into the action format, since word order varies by language. The key display name comes from the VM.
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
