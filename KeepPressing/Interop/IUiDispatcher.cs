using System;
using Microsoft.UI.Dispatching;

namespace KeepPressing.Interop;

/// <summary>
/// UI スレッドへの搬送ポート。Interop/Core から任意スレッドで届くイベントを UI スレッドで処理するために使う。
/// テスト時は同期実行の Fake に差し替えてマーシャリングを単純化できる。
/// </summary>
public interface IUiDispatcher
{
    /// <summary>指定のアクションを UI スレッドのキューに投函する。</summary>
    void Post(Action action);
}

/// <summary><see cref="DispatcherQueue"/> による実装。</summary>
public sealed class DispatcherQueueUiDispatcher(DispatcherQueue queue) : IUiDispatcher
{
    public void Post(Action action) => queue.TryEnqueue(() => action());
}
