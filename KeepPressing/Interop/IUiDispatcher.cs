using System;
using Microsoft.UI.Dispatching;

namespace KeepPressing.Interop;

/// <summary>Marshals work onto the UI thread. Tests swap in a synchronous fake.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class DispatcherQueueUiDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    public void Post(Action action) => queue.TryEnqueue(() => action());
}
