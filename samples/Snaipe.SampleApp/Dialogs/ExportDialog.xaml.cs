// samples/Snaipe.SampleApp/Dialogs/ExportDialog.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.SampleApp.ViewModels;

namespace Snaipe.SampleApp.Dialogs;

public sealed partial class ExportDialog : ContentDialog
{
    private readonly ExportViewModel _vm;

    public ExportDialog(ExportViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = _vm;

        PrimaryButtonClick += (_, _) => _ = _vm.ExportCommand.ExecuteAsync();
    }

    private void OnBrowseClicked(object sender, RoutedEventArgs e)
        => _ = _vm.BrowseCommand.ExecuteAsync();
}
