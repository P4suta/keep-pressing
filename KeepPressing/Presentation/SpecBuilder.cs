using System;
using KeepPressing.Core;
using KeepPressing.ViewModels;

namespace KeepPressing.Presentation;

/// <summary>UI の選択状態のスナップショット（<see cref="SpecBuilder"/> の入力）。</summary>
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
/// UI の選択状態（<see cref="PressConfiguration"/>）をドメイン型（<see cref="PressSpec"/>）へ翻訳する純粋関数。
/// 副作用を持たず UI/Win32 にも依存しない。エラー文言は <see cref="ILocalizer"/> 経由で受け取るため、
/// 翻訳の単一点でありながら単体テスト可能（テストは Fake ローカライザを渡す）。
/// </summary>
public static class SpecBuilder
{
    public static bool TryBuild(PressConfiguration config, ILocalizer loc, out PressSpec spec, out string? error)
    {
        // NumberBox は空欄のとき NaN を返すため、ドメイン型へ翻訳する前にここで弾く。
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
