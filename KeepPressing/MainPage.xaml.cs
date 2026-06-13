using System;
using KeepPressing.Interop;
using KeepPressing.ViewModels;
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
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = new MainPageViewModel(App.Services, App.DispatcherQueue);
        InitializeComponent();

        // 対象タブ（SelectorBar）の初期選択を ViewModel に合わせる（永続化された設定を反映）。
        var targetIndex = Math.Clamp(ViewModel.TargetKindIndex, 0, TargetSelector.Items.Count - 1);
        TargetSelector.SelectedItem = TargetSelector.Items[targetIndex];

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                // キー設定モード中は IME を外す。IME が ON だと文字キーが composition に
                // 取り込まれて KeyDown が届かず、キーを割り当てられないため（view の関心事なのでここで扱う）。
                case nameof(MainPageViewModel.IsKeyCaptureArmed):
                    if (ViewModel.IsKeyCaptureArmed)
                    {
                        ImeGuard.Suspend(App.WindowHandle);
                    }
                    else
                    {
                        ImeGuard.Restore(App.WindowHandle);
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

    // SelectorBar はインデックスを直接バインドできないため、選択を ViewModel.TargetKindIndex へ橋渡しする（view の関心事）。
    private void OnTargetSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var index = sender.Items.IndexOf(sender.SelectedItem);
        if (index >= 0)
        {
            ViewModel.TargetKindIndex = index;
        }
    }
}
