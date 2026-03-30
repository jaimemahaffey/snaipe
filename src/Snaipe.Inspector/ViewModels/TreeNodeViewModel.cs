using System.Collections.ObjectModel;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class TreeNodeViewModel : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public TreeNodeViewModel(ElementNode node)
    {
        Node = node;
        DisplayName = string.IsNullOrEmpty(node.Name)
            ? node.TypeName
            : $"{node.TypeName} \"{node.Name}\"";
    }

    public ElementNode Node { get; }
    public string DisplayName { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
