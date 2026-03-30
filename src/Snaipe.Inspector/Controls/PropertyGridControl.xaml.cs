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
    }

    // Safe as-cast: x:Bind evaluates before DataContext is set during XAML init;
    // hard cast would throw at startup. DataContextChanged + Bindings.Update() handles
    // the refresh once DataContext is properly assigned.
    public MainViewModel? ViewModel => DataContext as MainViewModel;
}
