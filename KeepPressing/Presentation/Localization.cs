using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace KeepPressing.Presentation;

/// <summary>
/// Localization abstraction for display strings. The VM, <see cref="SpecBuilder"/>, and <see cref="SpecDescriber"/>
/// take it as a parameter so they stay testable, decoupled from WinAppSDK's MRT/PRI.
/// </summary>
public interface ILocalizer
{
    string GetString(string key);

    /// <summary>Formats the resource's composite format string with the given arguments.</summary>
    string Format(string key, params object[] args);
}

/// <summary>
/// <see cref="ILocalizer"/> backed by MRT Core (resources.pri). Unpackaged apps have no default view, so the
/// default PRI path is passed explicitly via <see cref="ResourceLoader.GetDefaultResourceFilePath"/>; the
/// language resolves to the system display language.
/// </summary>
internal sealed class ResourceStringLocalizer : ILocalizer
{
    private readonly ResourceLoader _loader = new(ResourceLoader.GetDefaultResourceFilePath());

    public string GetString(string key) => _loader.GetString(key);

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, _loader.GetString(key), args);
}
