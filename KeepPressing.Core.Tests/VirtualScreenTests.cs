namespace KeepPressing.Core.Tests;

public sealed class VirtualScreenTests
{
    [Theory]
    [InlineData(-1920, 0)]      // Virtual screen left edge -> 0
    [InlineData(0, 32768)]      // Primary origin (center) -> 32768
    [InlineData(1919, 65518)]   // Rightmost pixel -> below 65535
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

        Assert.Equal(21845, nx);   // 65536 / 3 = 21845.33… truncated
        Assert.Equal(43690, ny);   // 131072 / 3 = 43690.67… truncated
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
