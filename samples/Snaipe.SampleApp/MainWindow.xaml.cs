// samples/Snaipe.SampleApp/MainWindow.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Snaipe.SampleApp.ViewModels;
using Snaipe.SampleApp.Windows;
using Snaipe.SampleApp.Dialogs;

namespace Snaipe.SampleApp;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _vm;
    private AsciiPreviewWindow? _previewWindow;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ASCII Studio";

        _vm = new ShellViewModel(DispatcherQueue);
        if (Content is FrameworkElement fe) fe.DataContext = _vm;

        // Wire up VM requests
        _vm.RequestOpenPreviewWindow  = OpenPreviewWindow;
        _vm.RequestClosePreviewWindow = ClosePreviewWindow;
        _vm.RequestShowExportDialog   = ShowExportDialog;

        Closed += (_, _) =>
        {
            _previewWindow?.Close();
            _vm.Dispose();
        };
    }

    private void OnToggleGridClicked(object sender, PointerRoutedEventArgs e)
    {
        SettingsFlyout.Visibility = SettingsFlyout.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenPreviewWindow()
    {
        if (_previewWindow is not null) return;
        _previewWindow = new AsciiPreviewWindow(_vm.AsciiOutput);
        _previewWindow.Closed += (_, _) =>
        {
            _previewWindow = null;
            _vm.NotifyPreviewWindowClosed();
        };
        _previewWindow.Activate();
        _vm.NotifyPreviewWindowOpened();
    }

    private void ClosePreviewWindow()
    {
        _previewWindow?.Close();
        _previewWindow = null;
    }

    private async void ShowExportDialog()
    {
        var dialog = new ExportDialog(_vm.Export);
        if (this.Content?.XamlRoot is { } root) dialog.XamlRoot = root;
        await dialog.ShowAsync();
    }
}
