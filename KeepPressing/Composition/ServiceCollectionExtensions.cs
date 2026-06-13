using KeepPressing.Core;
using KeepPressing.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace KeepPressing.Composition;

/// <summary>
/// DI コンテナへのサービス登録の単一点（合成ルート）。
/// 入力合成・ホットキー・カーソル取得・エンジンはいずれもプロセス内で 1 つの状態（専用スレッドや
/// 実行中の連打ループ）を所有するため Singleton で登録する。
/// </summary>
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKeepPressing(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IInputSynthesizer, Win32InputSynthesizer>();
        services.AddSingleton<PressEngine>();
        services.AddSingleton<HotkeyListener>();
        services.AddSingleton<ICursorLocator, CursorLocator>();
        return services;
    }
}
