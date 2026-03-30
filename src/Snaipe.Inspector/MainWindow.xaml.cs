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
    }

    // Typed accessor for x:Bind in this file.
    public MainViewModel ViewModel => _viewModel;
}
