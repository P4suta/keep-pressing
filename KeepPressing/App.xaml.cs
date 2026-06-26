using System;
using KeepPressing.Composition;
using KeepPressing.Core;
using KeepPressing.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace KeepPressing;

/// <summary>
/// アプリケーションのライフサイクルと合成ルート。
/// DI コンテナ（<see cref="Services"/>）がサービスの生成と破棄を所有する。
/// </summary>
public partial class App : Application
{
    private readonly WindowHandleProvider _windowHandle = new();
    private IServiceProvider _serviceProvider = null!;
    private bool _shutdownComplete;

    public App()
    {
        // タスクバーのピン留め/グルーピングを安定させる明示的 AUMID（unpackaged 既定の自動 AUMID 回避）。
        // 最初のウィンドウ表示前＝UI 生成前に設定する必要があるためコンストラクタ先頭で呼ぶ。
        _ = Windows.Win32.PInvoke.SetCurrentProcessExplicitAppUserModelID("P4suta.KeepPressing");
        InitializeComponent();
    }

    /// <summary>
    /// 合成ルートの <see cref="IServiceProvider"/>。WinUI の <see cref="Page"/> は引数なし
    /// コンストラクタで生成され ViewModel を直接注入できないため、Page はこのプロパティ
    /// （Composition Root への単一のアクセス点）から自身の ViewModel を解決する。
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // DispatcherQueue は UI スレッドで取得した値をインスタンス登録する
        // （ファクトリ遅延解決は別スレッドのキューを掴む危険があるため）。
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _serviceProvider = new ServiceCollection()
            .AddSingleton(dispatcher)
            .AddSingleton<IWindowHandleProvider>(_windowHandle)
            .AddKeepPressing()
            .BuildServiceProvider();

        // 最終安全網: 未処理例外でも長押しの Up を送出してから落ちる。
        // Core は ConfigureAwait(false) を徹底している（CA2007=error）ため、
        // この同期待機はスレッドプール上の継続を待つだけでデッドロックしない。
        UnhandledException += (_, _) =>
        {
            try
            {
                _serviceProvider.GetRequiredService<PressEngine>().StopAsync().Wait(500);
            }
            catch
            {
                // 終了経路 — これ以上できることはない。
            }
        };

        var window = new MainWindow();
        _windowHandle.Attach(window);   // HWND は Window 生成後に供給する（IImeGuard 等が参照）。
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;   // 2 回目はそのまま閉じる。
        }

        // 非同期クローズ: UI スレッドを塞がずに DI 所有の Disposable を一括破棄する。
        // ServiceProvider は登録の逆順で破棄し、PressEngine.DisposeAsync が長押しの Up 送出完了まで待つ。
        e.Handled = true;
        await ((IAsyncDisposable)_serviceProvider).DisposeAsync();
        _shutdownComplete = true;
        ((Window)sender).Close();
    }
}
