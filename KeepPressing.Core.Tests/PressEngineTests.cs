using Microsoft.Extensions.Time.Testing;

namespace KeepPressing.Core.Tests;

public sealed class PressEngineTests
{
    private static readonly InputTarget LeftClick = new InputTarget.Mouse(MouseButton.Left);

    private static PressSpec Repeat50(InputTarget? target = null) =>
        new(target ?? LeftClick, new PressMode.Repeat(TimeSpan.FromMilliseconds(50)));

    private static PressSpec Hold(InputTarget? target = null) =>
        new(target ?? LeftClick, PressMode.Hold.Instance);

    [Fact]
    public async Task Repeat_TapsImmediatelyOnStart()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());

        engine.Start(Repeat50());

        // do-while のため 1 打目は Start 内で同期送出される。
        (InputAction, InputTarget)[] expected = [(InputAction.Press, LeftClick), (InputAction.Release, LeftClick)];
        Assert.Equal(expected, fake.Log);
    }

    [Fact]
    public async Task Repeat_TapsOnEachInterval()
    {
        var fake = new FakeInputSynthesizer();
        var time = new FakeTimeProvider();
        await using var engine = new PressEngine(fake, time);
        engine.Start(Repeat50());

        time.Advance(TimeSpan.FromMilliseconds(49));   // 境界の手前 — tick しない
        await Task.Delay(50);
        Assert.Equal(2, fake.Log.Count);

        time.Advance(TimeSpan.FromMilliseconds(1));    // ちょうど 50ms — 2 打目
        await fake.WaitForCountAsync(4);

        time.Advance(TimeSpan.FromMilliseconds(50));   // 3 打目
        await fake.WaitForCountAsync(6);
    }

    [Fact]
    public async Task Repeat_StopPreventsFurtherTaps()
    {
        var fake = new FakeInputSynthesizer();
        var time = new FakeTimeProvider();
        await using var engine = new PressEngine(fake, time);
        engine.Start(Repeat50());

        await engine.StopAsync();
        time.Advance(TimeSpan.FromMilliseconds(500));
        await Task.Delay(50);

        Assert.Equal(2, fake.Log.Count);
    }

    [Fact]
    public async Task Repeat_PressReleaseAlwaysPaired()
    {
        var fake = new FakeInputSynthesizer();
        var time = new FakeTimeProvider();
        await using var engine = new PressEngine(fake, time);
        engine.Start(Repeat50());

        time.Advance(TimeSpan.FromMilliseconds(50));
        await fake.WaitForCountAsync(4);
        time.Advance(TimeSpan.FromMilliseconds(50));
        await fake.WaitForCountAsync(6);
        await engine.StopAsync();

        var log = fake.Log;
        Assert.Equal(0, log.Count % 2);
        for (var i = 0; i < log.Count; i += 2)
        {
            Assert.Equal(InputAction.Press, log[i].Action);
            Assert.Equal(InputAction.Release, log[i + 1].Action);
        }
    }

    [Fact]
    public async Task Hold_PressesOnStart_NoRelease()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());

        engine.Start(Hold());

        (InputAction, InputTarget)[] expected = [(InputAction.Press, LeftClick)];
        Assert.Equal(expected, fake.Log);
    }

    [Fact]
    public async Task Hold_ReleasesExactlyOnceOnStop()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());
        engine.Start(Hold());

        await engine.StopAsync();

        // 「StopAsync 完了 = Up 送出済み」の API 契約。
        (InputAction, InputTarget)[] expected = [(InputAction.Press, LeftClick), (InputAction.Release, LeftClick)];
        Assert.Equal(expected, fake.Log);
    }

    [Fact]
    public async Task Hold_ReleasesOnDisposeAsync()
    {
        var fake = new FakeInputSynthesizer();
        var engine = new PressEngine(fake, new FakeTimeProvider());
        engine.Start(Hold());

        await engine.DisposeAsync();

        (InputAction, InputTarget)[] expected = [(InputAction.Press, LeftClick), (InputAction.Release, LeftClick)];
        Assert.Equal(expected, fake.Log);
    }

    [Fact]
    public async Task Start_WhileRunning_Throws()
    {
        await using var engine = new PressEngine(new FakeInputSynthesizer(), new FakeTimeProvider());
        engine.Start(Hold());

        Assert.Throws<InvalidOperationException>(() => engine.Start(Hold()));
    }

    [Fact]
    public async Task Stop_WhileIdle_IsNoOp()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());

        await engine.StopAsync();

        Assert.Empty(fake.Log);
        Assert.Same(EngineState.Idle.Instance, engine.State);
    }

    [Fact]
    public async Task StateChanged_RaisesRunningThenIdle_WithSpec()
    {
        await using var engine = new PressEngine(new FakeInputSynthesizer(), new FakeTimeProvider());
        var states = new List<EngineState>();
        engine.StateChanged += states.Add;
        var spec = Repeat50();

        engine.Start(spec);
        await engine.StopAsync();

        EngineState[] expected = [new EngineState.Running(spec), EngineState.Idle.Instance];
        Assert.Equal(expected, states);   // record 等値性で Spec ごと比較
    }

    [Fact]
    public async Task TargetFlowsThroughUnchanged()
    {
        InputTarget[] targets =
        [
            new InputTarget.Mouse(MouseButton.Right, new ScreenPoint(100, 200)),  // 固定座標
            new InputTarget.Mouse(MouseButton.Middle),                            // 現在カーソル位置
            new InputTarget.Key(new KeyCode(0x41)),                               // キー
        ];

        foreach (var target in targets)
        {
            var fake = new FakeInputSynthesizer();
            await using var engine = new PressEngine(fake, new FakeTimeProvider());

            engine.Start(new PressSpec(target, new PressMode.Repeat(TimeSpan.FromMilliseconds(50))));
            await engine.StopAsync();

            Assert.All(fake.Log, entry => Assert.Equal(target, entry.Target));
            Assert.NotEmpty(fake.Log);
        }
    }

    [Fact]
    public void RepeatMode_Ctor_RejectsNonPositiveInterval()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PressMode.Repeat(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PressMode.Repeat(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public async Task Faulted_RaisedWhenSynthesizerThrows()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());
        var boom = new InvalidOperationException("boom");
        fake.ThrowOnTap = boom;
        Exception? captured = null;
        engine.Faulted += ex => captured = ex;

        engine.Start(Repeat50());

        Assert.Same(boom, captured);                        // 1 打目は Start 内で走るため同期発火
        Assert.IsType<EngineState.Running>(engine.State);   // 遷移の所有権は StopAsync — まだ Running

        await engine.StopAsync();
        Assert.Same(EngineState.Idle.Instance, engine.State);

        fake.ThrowOnTap = null;                             // 故障解除後は再 Start 可能
        engine.Start(Repeat50());
        await fake.WaitForCountAsync(2);
    }

    [Fact]
    public async Task Faulted_ThenStop_RaisesIdleAfterFaultEvent()
    {
        var fake = new FakeInputSynthesizer();
        await using var engine = new PressEngine(fake, new FakeTimeProvider());
        fake.ThrowOnTap = new InvalidOperationException("boom");
        var events = new List<string>();
        engine.Faulted += _ => events.Add("faulted");
        engine.StateChanged += s => events.Add(s is EngineState.Running ? "running" : "idle");

        engine.Start(Repeat50());
        await engine.StopAsync();

        string[] expected = ["running", "faulted", "idle"];
        Assert.Equal(expected, events);
    }
}
