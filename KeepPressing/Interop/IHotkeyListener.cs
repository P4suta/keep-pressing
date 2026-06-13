using System;
using System.Threading.Tasks;

namespace KeepPressing.Interop;

/// <summary>
/// グローバルホットキーの登録・受信のポート。実装（<see cref="HotkeyListener"/>）を
/// テスト時に Fake へ差し替えられるようにする。
/// </summary>
public interface IHotkeyListener
{
    /// <summary>ホットキー押下。リスナースレッド上で発火する — 購読側が UI スレッドへマーシャリングすること。</summary>
    event Action<HotkeyId>? Pressed;

    /// <summary>ホットキーを登録する。他アプリと競合している場合は false を返す。どのスレッドからでも呼べる。</summary>
    Task<bool> RegisterAsync(HotkeyId id, HotkeyModifiers modifiers, ushort vk);

    /// <summary>ホットキーの登録を解除する。</summary>
    Task UnregisterAsync(HotkeyId id);
}
