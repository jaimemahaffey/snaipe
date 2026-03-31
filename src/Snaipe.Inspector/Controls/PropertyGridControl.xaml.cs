// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    public PropertyGridControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
        SearchBox.TextChanged += OnSearchTextChanged;
    }

    // DataContext is PropertyGridViewModel (set from MainWindow via x:Bind ViewModel.PropertyGrid).
    public PropertyGridViewModel? ViewModel => DataContext as PropertyGridViewModel;

    /// <summary>
    /// Reference to the MainViewModel. Set from MainWindow via x:Bind.
    /// Provides Breadcrumb, DrillIntoCommand, and NavigateToBreadcrumbCommand bindings.
    /// </summary>
    public MainViewModel? Host
    {
        get => (MainViewModel?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public static readonly DependencyProperty HostProperty =
        DependencyProperty.Register(nameof(Host), typeof(MainViewModel),
            typeof(PropertyGridControl), new PropertyMetadata(null));

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.SearchText = SearchBox.Text;
    }
}
