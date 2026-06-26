namespace KeepPressing.Core.Tests;

internal enum InputAction
{
    Press,
    Release,
}

/// <summary>
/// Test fake recording Press/Release in order. Tap is implemented as Press then Release so paired
/// assertions read naturally, while <see cref="ThrowOnTap"/> allows fault injection.
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
    /// Waits until the log reaches <paramref name="count"/> entries. Timer continuations after
    /// FakeTimeProvider.Advance() run asynchronously on the thread pool, so wait with this rather than
    /// asserting synchronously right after Advance.
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
                throw new TimeoutException($"Log did not reach {count} entries (currently {Log.Count}).");
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
