using System.Diagnostics;

namespace KeepPressing.Core;

/// <summary>Idle/Running state machine driving repeat and hold input.</summary>
/// <remarks>
/// Threading: <see cref="Start"/> / <see cref="StopAsync"/> / <see cref="DisposeAsync"/> / <see cref="State"/>
/// must be called from a single thread (the UI thread in practice); the engine holds no locks.
/// <see cref="StateChanged"/> / <see cref="Faulted"/> may fire on any thread (Core uses ConfigureAwait(false)
/// throughout), so subscribers marshal to the UI thread.
/// </remarks>
public sealed class PressEngine(IInputSynthesizer synthesizer, TimeProvider timeProvider) : IAsyncDisposable
{
    private readonly IInputSynthesizer _synthesizer = synthesizer;
    private readonly TimeProvider _timeProvider = timeProvider;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public EngineState State { get; private set; } = EngineState.Idle.Instance;

    /// <summary>State transitions, raised only inside <see cref="Start"/> / <see cref="StopAsync"/>.</summary>
    public event Action<EngineState>? StateChanged;

    /// <summary>
    /// The loop died on its own (e.g. a synthesizer exception); Up was already sent in each mode's finally.
    /// Does not transition state — <see cref="StopAsync"/> owns transitions, and a subscriber calls it to return to Idle.
    /// </summary>
    public event Action<Exception>? Faulted;

    public void Start(PressSpec spec)
    {
        if (State is EngineState.Running)
        {
            throw new InvalidOperationException("Engine is already running.");
        }

        _cts = new CancellationTokenSource();
        Transition(new EngineState.Running(spec));   // Observable before any side effect, on the calling thread.
        _loop = RunAsync(spec, _cts.Token);          // The first press runs synchronously up to the first await.
    }

    /// <summary>Stops. On completion, a held Up is guaranteed to have been sent (API contract).</summary>
    public async Task StopAsync()
    {
        if (State is not EngineState.Running)
        {
            return;
        }

        await _cts!.CancelAsync().ConfigureAwait(false);
        await _loop!.ConfigureAwait(false);          // Await the finally's Release — this is what guarantees Up.
        _cts.Dispose();
        (_cts, _loop) = (null, null);
        Transition(EngineState.Idle.Instance);
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    private void Transition(EngineState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private async Task RunAsync(PressSpec spec, CancellationToken ct)
    {
        try
        {
            await (spec.Mode switch
            {
                PressMode.Repeat repeat => RepeatAsync(spec.Target, repeat.Interval, ct),
                PressMode.Hold => HoldAsync(spec.Target, ct),
                _ => throw new UnreachableException(),
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
        catch (Exception ex)
        {
            Faulted?.Invoke(ex);
        }
    }

    private async Task RepeatAsync(InputTarget target, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval, _timeProvider);
        do
        {
            _synthesizer.Tap(target);                // do-while: fire the first tap immediately on start.
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }

    private async Task HoldAsync(InputTarget target, CancellationToken ct)
    {
        _synthesizer.Press(target);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, _timeProvider, ct).ConfigureAwait(false);
        }
        finally
        {
            _synthesizer.Release(target);            // Always Up, on cancellation or exception.
        }
    }
}
