using System;
using KeepPressing.Core;
using KeepPressing.ViewModels;

namespace KeepPressing.Presentation;

/// <summary>Snapshot of the UI selection, input to <see cref="SpecBuilder"/>.</summary>
public sealed record PressConfiguration(
    TargetKind Target,
    MouseButton MouseButton,
    bool UseFixedPosition,
    double FixedX,
    double FixedY,
    KeyCode? CapturedKey,
    PressModeKind Mode,
    double IntervalMs);

/// <summary>
/// Pure function translating the UI selection (<see cref="PressConfiguration"/>) into the domain type
/// (<see cref="PressSpec"/>). No side effects, no UI/Win32 dependency; error strings come via
/// <see cref="ILocalizer"/>, so it stays unit-testable with a fake localizer.
/// </summary>
public static class SpecBuilder
{
    public static bool TryBuild(PressConfiguration config, ILocalizer loc, out PressSpec spec, out string? error)
    {
        // NumberBox returns NaN when empty, so reject it before translating to the domain type.
        if (config.Mode is PressModeKind.Repeat && double.IsNaN(config.IntervalMs))
        {
            (spec, error) = (null!, loc.GetString("Error_IntervalRequired"));
            return false;
        }

        InputTarget target;
        if (config.Target is TargetKind.Mouse)
        {
            if (config.UseFixedPosition && (double.IsNaN(config.FixedX) || double.IsNaN(config.FixedY)))
            {
                (spec, error) = (null!, loc.GetString("Error_FixedPosRequired"));
                return false;
            }

            var position = config.UseFixedPosition
                ? new ScreenPoint((int)config.FixedX, (int)config.FixedY)
                : (ScreenPoint?)null;
            target = new InputTarget.Mouse(config.MouseButton, position);
        }
        else if (config.CapturedKey is { } key)
        {
            target = new InputTarget.Key(key);
        }
        else
        {
            (spec, error) = (null!, loc.GetString("Error_KeyRequired"));
            return false;
        }

        PressMode mode = config.Mode is PressModeKind.Repeat
            ? new PressMode.Repeat(TimeSpan.FromMilliseconds(Math.Max(config.IntervalMs, 1)))
            : PressMode.Hold.Instance;
        (spec, error) = (new PressSpec(target, mode), null);
        return true;
    }
}
