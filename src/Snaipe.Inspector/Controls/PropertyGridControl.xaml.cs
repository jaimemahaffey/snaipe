// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
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

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.SearchText = SearchBox.Text;
    }
}
