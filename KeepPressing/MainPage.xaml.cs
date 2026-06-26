using System.Linq;
using KeepPressing.Interop;
using KeepPressing.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace KeepPressing;

/// <summary>
/// Main page. All logic lives in <see cref="MainPageViewModel"/>; this only mediates raw KeyDown capture
/// during key-capture mode, temporary IME suspension, and the InfoBar close action.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly IImeGuard _imeGuard;

    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        // WinUI pages are created via a parameterless ctor, so resolve the ViewModel and view-only services
        // from the composition root (App.Current.Services).
        var services = ((App)Application.Current).Services;
        ViewModel = services.GetRequiredService<MainPageViewModel>();
        _imeGuard = services.GetRequiredService<IImeGuard>();
        InitializeComponent();

        // Sync the SelectorBar's initial selection to ViewModel.SelectedTarget (matched by the Tag enum).
        // SelectorBarItem.Tag x:Bind is evaluated in the Loading phase (after InitializeComponent), so Tag is
        // unset in the ctor. Defer the initial sync to Loaded, and don't throw when there's no match.
        Loaded += OnInitialTargetSync;

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                // Suspend the IME during key-capture mode: with the IME on, character keys are swallowed into
                // composition and KeyDown never arrives, so no key can be assigned (a view concern).
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

                // While capturing a position, update the live coordinates per render frame (smoother than a
                // timer). Frame sync is a view concern, so manage the subscription here.
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

    // SelectorBarItem.Tag x:Bind is evaluated in the Loading phase (unset in the ctor), so defer the initial
    // target selection to Loaded. FirstOrDefault avoids throwing when there's no match.
    private void OnInitialTargetSync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnInitialTargetSync;
        TargetSelector.SelectedItem = TargetSelector.Items
            .OfType<SelectorBarItem>()
            .FirstOrDefault(item => item.Tag is TargetKind kind && kind == ViewModel.SelectedTarget);
    }

    private void OnCaptureFrameRendering(object? sender, object e) => ViewModel.RefreshLivePosition();

    private void OnPagePreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!ViewModel.IsKeyCaptureArmed)
        {
            return;
        }

        // Capturing in PreviewKeyDown (tunneling) intercepts the key before the focused control consumes
        // Space/Enter.
        ViewModel.OnKeyCaptured((ushort)e.Key, e.Key.ToString());
        e.Handled = true;
    }

    private void OnErrorBarCloseClicked(InfoBar sender, object args) => ViewModel.ClearError();

    // SelectorBar can't bind an arbitrary value directly, so bridge each item's Tag (a TargetKind) to
    // ViewModel.SelectedTarget, avoiding an implicit index contract.
    private void OnTargetSelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is SelectorBarItem { Tag: TargetKind kind })
        {
            ViewModel.SelectedTarget = kind;
        }
    }
}
