using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PreviewPaneControl : UserControl
{
    private MainViewModel? _subscribedVm;

    public PreviewPaneControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Safe as-cast: x:Bind evaluates before DataContext is set during XAML init.
    public MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        // Unsubscribe from old ViewModel to prevent subscription leak.
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribedVm = args.NewValue as MainViewModel;

        // Subscribe to new ViewModel so SelectedXxx properties refresh when SelectedNode changes.
        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedNode))
            Bindings.Update();
    }

    private TreeNodeViewModel? Selected => ViewModel?.SelectedNode;

    public string SelectedTypeName => Selected?.Node.TypeName ?? "—";
    public string SelectedName     => Selected?.Node.Name ?? "(none)";
    public string SelectedId       => Selected?.Node.Id ?? "—";
    public string SelectedBoundsX  => Selected?.Node.Bounds?.X.ToString("F1") ?? "—";
    public string SelectedBoundsY  => Selected?.Node.Bounds?.Y.ToString("F1") ?? "—";
    public string SelectedBoundsW  => Selected?.Node.Bounds?.Width.ToString("F1") ?? "—";
    public string SelectedBoundsH  => Selected?.Node.Bounds?.Height.ToString("F1") ?? "—";
}
