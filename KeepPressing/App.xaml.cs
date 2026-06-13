using System;
using KeepPressing.Composition;
using KeepPressing.Core;
using KeepPressing.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace KeepPressing;

/// <summary>アプリ全体のサービス。DI コンテナの代わりの明示的合成ルート。</summary>
public sealed record AppServices(PressEngine Engine, HotkeyListener Hotkeys, ICursorLocator Cursor);

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>アプリ全体のサービス（合成ルート）。</summary>
    public static AppServices Services { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// IME interop, and any WinRT interop that requires <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>DI コンテナ（合成ルート）。Step 3 で静的 <see cref="Services"/> を置き換える。</summary>
    private IServiceProvider _serviceProvider = null!;

    private bool _shutdownComplete;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // 順序が重要: DispatcherQueue → Services → Window。
        // MainWindow のコンストラクタが Frame.Navigate(MainPage) を実行し、
        // MainPage が App.Services / App.DispatcherQueue を参照するため。
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // DI 骨格（並走）: DispatcherQueue は UI スレッドで取得した値をインスタンス登録する
        // （ファクトリ遅延解決は別スレッドのキューを掴む危険があるため）。
        _serviceProvider = new ServiceCollection()
            .AddSingleton(DispatcherQueue)
            .AddKeepPressing()
            .BuildServiceProvider();

        // 既存の静的 AppServices は ServiceProvider 解決の同一インスタンスを指す（インスタンスは 1 つ）。
        // Step 3 で AppServices record ごと廃止し、利用側を ServiceProvider 注入へ切り替える。
        Services = new AppServices(
            _serviceProvider.GetRequiredService<PressEngine>(),
            _serviceProvider.GetRequiredService<HotkeyListener>(),
            _serviceProvider.GetRequiredService<ICursorLocator>());

        // 最終安全網: 未処理例外でも長押しの Up を送出してから落ちる。
        // Core は ConfigureAwait(false) を徹底している（CA2007=error）ため、
        // この同期待機はスレッドプール上の継続を待つだけでデッドロックしない。
        UnhandledException += (_, _) =>
        {
            try
            {
                Services.Engine.StopAsync().Wait(500);
            }
            catch
            {
                // 終了経路 — これ以上できることはない。
            }
        };

        Window = new MainWindow();
        Window.Closed += OnWindowClosed;
        Window.Activate();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;   // 2 回目はそのまま閉じる。
        }

        // 非同期クローズ: 一旦キャンセルし、UI スレッドを塞がずに
        // エンジン停止（長押しの Up 送出を含む）を待ってから閉じ直す。
        e.Handled = true;
        await Services.Engine.DisposeAsync();
        Services.Hotkeys.Dispose();
        _shutdownComplete = true;
        Window.Close();
    }
}
