using System;
using KeepPressing.Composition;
using KeepPressing.Core;
using KeepPressing.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace KeepPressing;

/// <summary>App lifecycle and composition root. The DI container (<see cref="Services"/>) owns service creation and disposal.</summary>
public partial class App : Application
{
    private readonly WindowHandleProvider _windowHandle = new();
    private IServiceProvider _serviceProvider = null!;
    private bool _shutdownComplete;

    public App()
    {
        // Explicit AUMID for stable taskbar pinning/grouping (avoids the auto AUMID of unpackaged apps).
        // Must be set before the first window, i.e. before any UI, hence at the top of the constructor.
        _ = Windows.Win32.PInvoke.SetCurrentProcessExplicitAppUserModelID("P4suta.KeepPressing");
        InitializeComponent();
    }

    /// <summary>
    /// The composition root's <see cref="IServiceProvider"/>. WinUI <see cref="Page"/>s are created via a
    /// parameterless constructor and cannot take injected dependencies, so a page resolves its own ViewModel
    /// from this single access point.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Register the DispatcherQueue captured on the UI thread as an instance (lazy factory resolution
        // could grab another thread's queue).
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _serviceProvider = new ServiceCollection()
            .AddSingleton(dispatcher)
            .AddSingleton<IWindowHandleProvider>(_windowHandle)
            .AddKeepPressing()
            .BuildServiceProvider();

        // Last-resort safety net: send a held Up even on an unhandled exception before going down. Core
        // enforces ConfigureAwait(false) (CA2007=error), so this synchronous wait only awaits a thread-pool
        // continuation and cannot deadlock.
        UnhandledException += (_, _) =>
        {
            try
            {
                _serviceProvider.GetRequiredService<PressEngine>().StopAsync().Wait(500);
            }
            catch
            {
                // Shutdown path — nothing more we can do.
            }
        };

        var window = new MainWindow();
        _windowHandle.Attach(window);   // Supply the HWND after the window exists (used by IImeGuard, etc.).
        window.Closed += OnWindowClosed;
        window.Activate();
    }

    private async void OnWindowClosed(object sender, WindowEventArgs e)
    {
        if (_shutdownComplete)
        {
            return;   // Second pass: just close.
        }

        // Async close: dispose DI-owned disposables without blocking the UI thread. The ServiceProvider
        // disposes in reverse registration order, and PressEngine.DisposeAsync awaits the held Up.
        e.Handled = true;
        await ((IAsyncDisposable)_serviceProvider).DisposeAsync();
        _shutdownComplete = true;
        ((Window)sender).Close();
    }
}
