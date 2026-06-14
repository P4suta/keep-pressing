using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace KeepPressing.Presentation;

/// <summary>
/// 表示文字列のローカライズ抽象。VM・<see cref="SpecBuilder"/>・<see cref="SpecDescriber"/> はこれに依存し、
/// 実体（WinAppSDK の MRT/PRI）から切り離してテスト可能にする（純粋関数の引数として受け取る）。
/// </summary>
public interface ILocalizer
{
    /// <summary>リソースキーに対応する文字列を返す。</summary>
    string GetString(string key);

    /// <summary>リソースキーの複合書式文字列に引数を差し込んで返す。</summary>
    string Format(string key, params object[] args);
}

/// <summary>
/// MRT Core（resources.pri）から文字列を引く <see cref="ILocalizer"/> の実体。
/// unpackaged 配布では「既定ビュー」が無いため、<see cref="ResourceLoader.GetDefaultResourceFilePath"/> で
/// 既定 PRI パスを明示して構築する。言語はシステム表示言語で解決される（unpackaged の MRT 既定挙動）。
/// </summary>
internal sealed class ResourceStringLocalizer : ILocalizer
{
    private readonly ResourceLoader _loader = new(ResourceLoader.GetDefaultResourceFilePath());

    public string GetString(string key) => _loader.GetString(key);

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, _loader.GetString(key), args);
}
