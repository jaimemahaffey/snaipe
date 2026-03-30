// samples/Snaipe.SampleApp/MainWindow.xaml.cs
using Microsoft.UI.Xaml;

namespace Snaipe.SampleApp;

public sealed partial class MainWindow : Window
{
    private readonly SampleViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        // Window has no DataContext property in WinUI/Uno -- set on root content.
        if (Content is FrameworkElement root)
            root.DataContext = _viewModel;
        Title = "Snaipe Sample App";
    }

    public SampleViewModel ViewModel => _viewModel;
}
