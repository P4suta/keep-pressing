using System.Linq;
using KeepPressing.Interop;
using KeepPressing.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KeepPressing;

/// <summary>
/// メイン画面。ロジックはすべて <see cref="MainPageViewModel"/> にあり、
/// ここはキー設定モードの生 KeyDown 捕捉・IME の一時無効化・InfoBar の閉じる操作だけを仲介する。
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IImeGuard _imeGuard;

    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        // WinUI の Page は引数なし ctor で生成されるため、合成ルート（App.Current.Services）から
        // 自身の ViewModel と View 専用サービスを解決する（Composition Root への単一アクセス点）。
        var services = ((App)Application.Current).Services;
        ViewModel = services.GetRequiredService<MainPageViewModel>();
        _imeGuard = services.GetRequiredService<IImeGuard>();
        InitializeComponent();

        // 対象タブ（SelectorBar）の初期選択を ViewModel.SelectedTarget に合わせる（Tag の enum で対応付け）。
        TargetSelector.SelectedItem = TargetSelector.Items
            .OfType<SelectorBarItem>()
            .First(item => item.Tag is TargetKind kind && kind == ViewModel.SelectedTarget);

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                // キー設定モード中は IME を外す。IME が ON だと文字キーが composition に
                // 取り込まれて KeyDown が届かず、キーを割り当てられないため（view の関心事なのでここで扱う）。
                case nameof(MainPageViewModel.IsKeyCaptureArmed):
                    if (ViewModel.IsKeyCaptureArmed)
                    {
                        _imeGuard.Suspend();
                    }
                    else
                    {
                        _imeGuard.Restore();
                    }

                    break;

                // 座標キャプチャ中は描画フレームごとにライブ座標を更新する（タイマーより滑らか）。
                // フレーム同期は view の関心事なのでここで購読を管理する。
                case nameof(MainPageViewModel.IsCapturingPosition):
                    if (ViewModel.IsCapturingPosition)
                    {
                        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += OnCaptureFrameRendering;
                    }
                    else
                    {
                        Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= OnCaptureFrameRendering;
                    }

                    break;
            }
        };
    }

    private void OnCaptureFrameRendering(object? sender, object e) => ViewModel.RefreshLivePosition();

    private void OnPagePreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.IsKeyCaptureArmed)
        {
            return;
        }

        // PreviewKeyDown（トンネリング）で捕捉するため、フォーカス中のコントロールが
        // Space/Enter を消費する前に横取りできる。
        ViewModel.OnKeyCaptured((ushort)e.Key, e.Key.ToString());
        e.Handled = true;
    }

    private void OnErrorBarCloseClicked(InfoBar sender, object args) => ViewModel.ClearError();

    // SelectorBar は任意の値を直接バインドできないため、各項目の Tag に持たせた TargetKind を
    // ViewModel.SelectedTarget へ橋渡しする（view の関心事。インデックスの暗黙契約を排除）。
    private void OnTargetSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is SelectorBarItem { Tag: TargetKind kind })
        {
            ViewModel.SelectedTarget = kind;
        }
    }
}
