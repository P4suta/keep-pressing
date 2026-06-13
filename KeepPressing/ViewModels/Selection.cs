using Windows.System;

namespace KeepPressing.ViewModels;

/// <summary>
/// 連打対象の種別。UI の「選択途中の状態」を表すため Core の ADT（<c>InputTarget</c>）とは別に持つ
/// — キーが未設定でも「キーボード」タブは選べる必要があるため、確定済みの ADT インスタンスでは表せない。
/// </summary>
public enum TargetKind
{
    Mouse,
    Keyboard,
}

/// <summary>動作モードの種別。UI の選択状態（確定時に <c>PressMode</c> ADT へ翻訳する）。</summary>
public enum PressModeKind
{
    Repeat,
    Hold,
}

/// <summary>
/// ComboBox 等の選択肢。値（型）と表示名の対。XAML は <c>ItemsSource</c> でこのリストをバインドし、
/// <c>DisplayMemberPath="DisplayName"</c> で表示する。これにより項目の並びを XAML に重複させない。
/// </summary>
public sealed record Choice<T>(T Value, string DisplayName);

/// <summary>
/// 開始/停止ホットキーの選択肢。VK と表示名（"F5" 等）の対。
/// 並行配列＋XAML リテラルの 3 重定義を 1 つの型付きソースに統合する。表示名は言語非依存の記号。
/// </summary>
public sealed record HotkeyChoice(VirtualKey Vk, string DisplayKey);
