// src/Snaipe.Inspector/MainWindow.xaml.cs
using Microsoft.UI.Xaml;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        // Window is not a FrameworkElement in WinUI/Uno — set DataContext on the
        // root content element so UserControls can inherit it via DataContext cast.
        if (Content is FrameworkElement root)
            root.DataContext = _viewModel;
    }

    // Typed accessor for x:Bind in this file.
    public MainViewModel ViewModel => _viewModel;
}
