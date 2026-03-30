// src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ElementTreeControl : UserControl
{
    public ElementTreeControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ElementTree.ItemInvoked += OnItemInvoked;
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    private void OnDataContextChanged(Microsoft.UI.Xaml.FrameworkElement sender,
        Microsoft.UI.Xaml.DataContextChangedEventArgs args)
    {
        Bindings.Update();
        if (args.NewValue is MainViewModel vm)
        {
            vm.RootNodes.CollectionChanged += OnRootNodesChanged;
            RebuildTree(vm.RootNodes);
        }
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildTree(ViewModel.RootNodes);

    private void RebuildTree(ObservableCollection<TreeNodeViewModel> roots)
    {
        ElementTree.RootNodes.Clear();
        foreach (var root in roots)
            ElementTree.RootNodes.Add(BuildNode(root));
    }

    private static TreeViewNode BuildNode(TreeNodeViewModel vm)
    {
        var node = new TreeViewNode
        {
            Content = vm,
            IsExpanded = vm.IsExpanded,
        };
        foreach (var child in vm.Children)
            node.Children.Add(BuildNode(child));
        return node;
    }

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
            ViewModel.SelectedNode = vm;
    }
}
