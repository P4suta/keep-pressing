namespace KeepPressing.Core.Tests;

internal enum InputAction
{
    Press,
    Release,
}

/// <summary>
/// テスト用の入力合成 Fake。Press/Release を順序付きで記録する。
/// Tap は DIM と同じ意味論（Press → Release）で実装し、対の検証を自然に書けるようにしつつ、
/// <see cref="ThrowOnTap"/> による故障注入を可能にする。
/// </summary>
internal sealed class FakeInputSynthesizer : IInputSynthesizer
{
    private readonly Lock _gate = new();
    private readonly List<(InputAction Action, InputTarget Target)> _log = [];

    public Exception? ThrowOnTap { get; set; }

    public IReadOnlyList<(InputAction Action, InputTarget Target)> Log
    {
        get
        {
            lock (_gate)
            {
                return [.. _log];
            }
        }
    }

    public void Press(InputTarget target) => Add(InputAction.Press, target);

    public void Release(InputTarget target) => Add(InputAction.Release, target);

    public void Tap(InputTarget target)
    {
        if (ThrowOnTap is { } ex)
        {
            throw ex;
        }

        Press(target);
        Release(target);
    }

    /// <summary>
    /// ログ件数が <paramref name="count"/> に達するまで待つ。
    /// FakeTimeProvider.Advance() 後のタイマー継続はスレッドプールで非同期に走るため、
    /// Advance 直後の同期アサートではなく必ずこれで到達を待つこと。
    /// </summary>
    public async Task WaitForCountAsync(int count, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(2));
        while (true)
        {
            lock (_gate)
            {
                if (_log.Count >= count)
                {
                    return;
                }
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"ログ件数が {count} に達しなかった（現在 {Log.Count} 件）。");
            }

            await Task.Delay(5);
        }
    }

    private void Add(InputAction action, InputTarget target)
    {
        lock (_gate)
        {
            _log.Add((action, target));
        }
    }
}
