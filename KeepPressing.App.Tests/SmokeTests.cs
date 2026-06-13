using KeepPressing.ViewModels;
using Windows.System;

namespace KeepPressing.App.Tests;

/// <summary>WinUI アセンブリ(KeepPressing)を参照したテストがビルド・実行できることの確認。</summary>
public class SmokeTests
{
    [Fact]
    public void HotkeyChoice_HasValueEquality()
    {
        Assert.Equal(new HotkeyChoice(VirtualKey.F6, "F6"), new HotkeyChoice(VirtualKey.F6, "F6"));
    }
}
