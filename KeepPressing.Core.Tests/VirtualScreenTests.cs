namespace KeepPressing.Core.Tests;

public sealed class VirtualScreenTests
{
    [Theory]
    [InlineData(-1920, 0)]      // 仮想スクリーン左端 → 0
    [InlineData(0, 32768)]      // プライマリ原点（中央）→ 32768
    [InlineData(1919, 65518)]   // 右端ピクセル → 65535 未満
    public void Normalize_HandlesNegativeOrigin(int x, int expectedNx)
    {
        var r = new VirtualScreenRect(-1920, 0, 3840, 1080);

        var (nx, _) = VirtualScreen.Normalize(new ScreenPoint(x, 0), r);

        Assert.Equal(expectedNx, nx);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(960, 32768)]
    [InlineData(1919, 65501)]
    public void Normalize_SingleMonitor(int x, int expectedNx)
    {
        var r = new VirtualScreenRect(0, 0, 1920, 1080);

        var (nx, _) = VirtualScreen.Normalize(new ScreenPoint(x, 0), r);

        Assert.Equal(expectedNx, nx);
    }

    [Fact]
    public void Normalize_ClampsOutOfRangeInput()
    {
        var r = new VirtualScreenRect(0, 0, 1920, 1080);

        Assert.Equal((0, 0), VirtualScreen.Normalize(new ScreenPoint(-100, -100), r));
        Assert.Equal((65535, 65535), VirtualScreen.Normalize(new ScreenPoint(5000, 5000), r));
    }

    [Fact]
    public void Normalize_TruncatesTowardZero()
    {
        var r = new VirtualScreenRect(0, 0, 3, 3);

        var (nx, ny) = VirtualScreen.Normalize(new ScreenPoint(1, 2), r);

        Assert.Equal(21845, nx);   // 65536 / 3 = 21845.33… → 切り捨て
        Assert.Equal(43690, ny);   // 131072 / 3 = 43690.67… → 切り捨て
    }

    [Fact]
    public void Normalize_RejectsNonPositiveExtent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VirtualScreen.Normalize(default, new VirtualScreenRect(0, 0, 0, 1080)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VirtualScreen.Normalize(default, new VirtualScreenRect(0, 0, 1920, -1)));
    }
}
