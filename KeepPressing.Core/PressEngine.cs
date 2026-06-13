using System.Diagnostics;

namespace KeepPressing.Core;

/// <summary>
/// 連打/長押しエンジン。<see cref="EngineState.Idle"/> / <see cref="EngineState.Running"/> の 2 状態機械。
/// </summary>
/// <remarks>
/// スレッド契約: <see cref="Start"/> / <see cref="StopAsync"/> / <see cref="DisposeAsync"/> / <see cref="State"/>
/// は単一スレッド（実運用では UI スレッド）から呼ぶこと。エンジンはロックを持たない。
/// <see cref="StateChanged"/> / <see cref="Faulted"/> は任意のスレッドで発火しうる
/// （Core は ConfigureAwait(false) を徹底するため）— 購読側が UI スレッドへマーシャリングする。
/// </remarks>
public sealed class PressEngine(IInputSynthesizer synthesizer, TimeProvider timeProvider) : IAsyncDisposable
{
    private readonly IInputSynthesizer _synthesizer = synthesizer;
    private readonly TimeProvider _timeProvider = timeProvider;

    private CancellationTokenSource? _cts;
    private Task? _loop;

    public EngineState State { get; private set; } = EngineState.Idle.Instance;

    /// <summary>状態遷移の通知。遷移は <see cref="Start"/> / <see cref="StopAsync"/> の中でのみ起きる。</summary>
    public event Action<EngineState>? StateChanged;

    /// <summary>
    /// ループが合成例外などで自然死したことの通知。Up は各モードの finally で送出済み。
    /// 状態遷移は行わない — 遷移の所有権は <see cref="StopAsync"/> にあり、購読側がそれを呼んで Idle へ戻す。
    /// </summary>
    public event Action<Exception>? Faulted;

    public void Start(PressSpec spec)
    {
        if (State is EngineState.Running)
        {
            throw new InvalidOperationException("エンジンは既に実行中。");
        }

        _cts = new CancellationTokenSource();
        Transition(new EngineState.Running(spec));   // 副作用より先に観測可能化（呼び出しスレッド上）
        _loop = RunAsync(spec, _cts.Token);          // 最初の打鍵は最初の await まで同期実行される（即応性）
    }

    /// <summary>停止する。完了時点で長押しの Up 送出まで済んでいることを API 契約として保証する。</summary>
    public async Task StopAsync()
    {
        if (State is not EngineState.Running)
        {
            return;
        }

        await _cts!.CancelAsync().ConfigureAwait(false);
        await _loop!.ConfigureAwait(false);          // finally の Release 完了まで待つ ＝ Up 保証の要
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
                _ => throw new UnreachableException(), // private ctor で閉じた階層 → 到達不能
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 正常停止。
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
            _synthesizer.Tap(target);                // do-while: 開始直後に 1 打目を即時送出
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
            _synthesizer.Release(target);            // キャンセル・例外いずれでも必ず Up
        }
    }
}
