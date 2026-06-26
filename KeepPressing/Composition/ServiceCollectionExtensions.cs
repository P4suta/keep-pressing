using KeepPressing.Core;
using KeepPressing.Interop;
using KeepPressing.Presentation;
using KeepPressing.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KeepPressing.Composition;

/// <summary>
/// Single registration point (composition root). Input synthesis, hotkeys, the cursor locator, and the
/// engine each own one piece of process-wide state (a dedicated thread or the running loop), so they are
/// registered as singletons.
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeepPressing(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInputSynthesizer, Win32InputSynthesizer>();
        services.AddSingleton<PressEngine>();
        services.AddSingleton<IHotkeyListener, HotkeyListener>();
        services.AddSingleton<ICursorLocator, CursorLocator>();
        services.AddSingleton<IImeGuard, ImeGuard>();
        services.AddSingleton<IUiDispatcher, DispatcherQueueUiDispatcher>();
        services.AddSingleton<ILocalizer, ResourceStringLocalizer>();
        services.AddTransient<MainPageViewModel>();
        return services;
    }
}
