namespace KeepPressing.Core;

/// <summary>1 回の実行セッションの完全な仕様（何を・どう押すか）。</summary>
public sealed record PressSpec(InputTarget Target, PressMode Mode);
