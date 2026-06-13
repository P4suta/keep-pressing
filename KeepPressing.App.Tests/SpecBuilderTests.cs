using KeepPressing.Core;
using KeepPressing.Presentation;
using KeepPressing.ViewModels;

namespace KeepPressing.App.Tests;

public class SpecBuilderTests
{
    private static PressConfiguration Mouse(MouseButton button, bool fixedPos = false, double x = 0, double y = 0) =>
        new(TargetKind.Mouse, button, fixedPos, x, y, null, PressModeKind.Repeat, 50);

    [Theory]
    [InlineData(MouseButton.Left)]
    [InlineData(MouseButton.Right)]
    [InlineData(MouseButton.Middle)]
    public void Build_Mouse_CurrentPosition_PreservesButton(MouseButton button)
    {
        Assert.True(SpecBuilder.TryBuild(Mouse(button), out var spec, out _));
        var m = Assert.IsType<InputTarget.Mouse>(spec.Target);
        Assert.Equal(button, m.Button);
        Assert.Null(m.Position);
    }

    [Fact]
    public void Build_Mouse_FixedPosition_SetsPoint()
    {
        Assert.True(SpecBuilder.TryBuild(Mouse(MouseButton.Left, fixedPos: true, x: 100, y: 200), out var spec, out _));
        var m = Assert.IsType<InputTarget.Mouse>(spec.Target);
        Assert.Equal(new ScreenPoint(100, 200), m.Position);
    }

    [Fact]
    public void Build_Mouse_FixedPositionWithNaN_Fails()
    {
        Assert.False(SpecBuilder.TryBuild(Mouse(MouseButton.Left, fixedPos: true, x: double.NaN), out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Build_KeyboardWithoutKey_Fails()
    {
        var config = new PressConfiguration(TargetKind.Keyboard, MouseButton.Left, false, 0, 0, null, PressModeKind.Repeat, 50);
        Assert.False(SpecBuilder.TryBuild(config, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Build_KeyboardWithKey_Succeeds()
    {
        var config = new PressConfiguration(TargetKind.Keyboard, MouseButton.Left, false, 0, 0, new KeyCode(0x41), PressModeKind.Repeat, 50);
        Assert.True(SpecBuilder.TryBuild(config, out var spec, out _));
        var k = Assert.IsType<InputTarget.Key>(spec.Target);
        Assert.Equal((ushort)0x41, k.Code.Value);
    }

    [Fact]
    public void Build_RepeatWithNaNInterval_Fails()
    {
        var config = new PressConfiguration(TargetKind.Mouse, MouseButton.Left, false, 0, 0, null, PressModeKind.Repeat, double.NaN);
        Assert.False(SpecBuilder.TryBuild(config, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Build_RepeatClampsNonPositiveIntervalToOne()
    {
        var config = new PressConfiguration(TargetKind.Mouse, MouseButton.Left, false, 0, 0, null, PressModeKind.Repeat, 0);
        Assert.True(SpecBuilder.TryBuild(config, out var spec, out _));
        var repeat = Assert.IsType<PressMode.Repeat>(spec.Mode);
        Assert.Equal(1, repeat.Interval.TotalMilliseconds);
    }

    [Fact]
    public void Build_HoldMode_ProducesHold()
    {
        var config = new PressConfiguration(TargetKind.Mouse, MouseButton.Left, false, 0, 0, null, PressModeKind.Hold, 50);
        Assert.True(SpecBuilder.TryBuild(config, out var spec, out _));
        Assert.IsType<PressMode.Hold>(spec.Mode);
    }
}
