// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    private PropertyGridViewModel? _subscribedVm;

    public PropertyGridControl()
    {
        InitializeComponent();

        // The Uno DataGrid's FEATURE_ICOLLECTIONVIEW_GROUP block is compiled out, so PropertyName
        // and PropertyValue are never set automatically for pre-grouped sources.
        // Set PropertyValue here from the ICollectionViewGroup.Group object (our PropertyCategoryGroup
        // whose ToString() returns the category key).
        PropertyDataGrid.LoadingRowGroup += (s, e) =>
            e.RowGroupHeader.PropertyValue = e.RowGroupHeader.CollectionViewGroup?.Group?.ToString();

        DataContextChanged += OnDataContextChanged;
        SearchBox.TextChanged += OnSearchTextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_subscribedVm is { } old)
            old.PropertiesRebuilt -= OnPropertiesRebuilt;

        Bindings.Update();

        _subscribedVm = ViewModel;

        if (_subscribedVm is { } vm)
        {
            vm.PropertiesRebuilt += OnPropertiesRebuilt;
            RefreshDataGridSource(vm);
        }
        else
        {
            PropertyDataGrid.ItemsSource = null;
        }
    }

    private void OnPropertiesRebuilt(object? sender, EventArgs e)
    {
        if (_subscribedVm is { } vm)
            RefreshDataGridSource(vm);
    }

    private void RefreshDataGridSource(PropertyGridViewModel vm)
    {
        // Tear down the old visual tree first so stale group headers don't linger.
        PropertyDataGrid.ItemsSource = null;

        if (vm.FilteredProperties.Count == 0) return;

        // Build a brand-new CVS each time so the DataGrid receives a fresh ICollectionView.
        // Reusing a single CVS and re-setting Source to the same ObservableCollection reference
        // can leave Uno's DataGrid with stale rows whose DataContext was never updated.
        var cvs = new CollectionViewSource
        {
            Source = vm.FilteredProperties,
            IsSourceGrouped = true,
        };
        PropertyDataGrid.ItemsSource = cvs.View;
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

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool b && b) ? 0.6 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
