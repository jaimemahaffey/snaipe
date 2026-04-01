// src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ElementTreeControl : UserControl
{
    private MainViewModel? _subscribedVm;
    private readonly Dictionary<TreeNodeViewModel, TreeViewNode> _nodeMap = new();

    public ElementTreeControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ElementTree.ItemInvoked += OnItemInvoked;
        ElementTree.Expanding += OnNodeExpanding;
        ElementTree.Collapsed += OnNodeCollapsed;
    }

    public MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_subscribedVm is not null)
        {
            _subscribedVm.RootNodes.CollectionChanged -= OnRootNodesChanged;
            _subscribedVm.ScrollIntoViewRequested -= OnScrollIntoViewRequested;
        }

        _subscribedVm = args.NewValue as MainViewModel;

        if (_subscribedVm is not null)
        {
            _subscribedVm.RootNodes.CollectionChanged += OnRootNodesChanged;
            _subscribedVm.ScrollIntoViewRequested += OnScrollIntoViewRequested;
            RebuildTree(_subscribedVm.RootNodes);
        }
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            RebuildTree(vm.RootNodes);
    }

    private void RebuildTree(System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel> roots)
    {
        _nodeMap.Clear();
        ElementTree.RootNodes.Clear();
        foreach (var root in roots)
            ElementTree.RootNodes.Add(BuildNode(root));
    }

    private TreeViewNode BuildNode(TreeNodeViewModel vm)
    {
        var node = new TreeViewNode
        {
            Content = vm,
            IsExpanded = vm.IsExpanded,
        };
        _nodeMap[vm] = node;
        foreach (var child in vm.Children)
            node.Children.Add(BuildNode(child));
        return node;
    }

    private void OnScrollIntoViewRequested(TreeNodeViewModel vm)
    {
        if (_nodeMap.TryGetValue(vm, out var tvNode))
        {
            var container = ElementTree.ContainerFromNode(tvNode) as FrameworkElement;
            container?.StartBringIntoView();
        }
    }

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
        {
            if (ViewModel is { } viewModel)
                viewModel.SelectedNode = vm;
        }
    }

    private void OnNodeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
            vm.IsExpanded = true;
    }

    private void OnNodeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Item is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
            vm.IsExpanded = false;
    }
}
