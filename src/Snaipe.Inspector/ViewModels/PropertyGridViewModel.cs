using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

public class PropertyCategoryGroup : ObservableCollection<PropertyRowViewModel>
{
    public string Key { get; }
    public PropertyCategoryGroup(string key, IEnumerable<PropertyRowViewModel> items) : base(items)
    {
        Key = key;
    }
}

public sealed class PropertyGridViewModel : ViewModelBase
{
    private List<PropertyRowViewModel> _allProperties = [];
    private string _searchText = "";
    private string _activeSortColumn = "Category";
    private bool _sortAscending = true;
    
    private PropertyRowViewModel? _activeChainRow;
    private ValueChainEntryViewModel[]? _activeValueChain;
    private string? _valueChainPropertyName;

    public PropertyGridViewModel()
    {
        ClearValueChainCommand = new RelayCommand(ClearValueChain);
    }

    public ObservableCollection<PropertyCategoryGroup> FilteredProperties { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                RebuildFilteredProperties();
        }
    }

    public RelayCommand ClearValueChainCommand { get; }

    public ValueChainEntryViewModel[]? ActiveValueChain
    {
        get => _activeValueChain;
        private set
        {
            if (SetField(ref _activeValueChain, value))
                OnPropertyChanged(nameof(ValueChainPanelVisibility));
        }
    }

    public string? ValueChainPropertyName
    {
        get => _valueChainPropertyName;
        private set => SetField(ref _valueChainPropertyName, value);
    }

    public Microsoft.UI.Xaml.Visibility ValueChainPanelVisibility =>
        _activeValueChain is not null
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public void ShowValueChain(PropertyRowViewModel row)
    {
        if (row.ValueChain is null) return;

        if (ReferenceEquals(_activeChainRow, row) && ActiveValueChain is not null)
        {
            ClearValueChain();
            return;
        }

        _activeChainRow = row;
        ActiveValueChain = row.ValueChain.ToArray();
        ValueChainPropertyName = $"{row.Entry.Name} — value chain";
    }

    public void ClearValueChain()
    {
        _activeChainRow = null;
        ActiveValueChain = null;
        ValueChainPropertyName = null;
    }

    public void Load(IEnumerable<PropertyRowViewModel> rows)
    {
        _allProperties = rows.ToList();
        RebuildFilteredProperties();
    }

    public void Clear()
    {
        _allProperties.Clear();
        FilteredProperties.Clear();
        ClearValueChain();
    }

    private void RebuildFilteredProperties()
    {
        IEnumerable<PropertyRowViewModel> rows = string.IsNullOrEmpty(_searchText)
            ? _allProperties
            : _allProperties.Where(r => r.Entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        IEnumerable<PropertyRowViewModel> sorted = _activeSortColumn switch
        {
            "Name"     => _sortAscending ? rows.OrderBy(r => r.Entry.Name)           : rows.OrderByDescending(r => r.Entry.Name),
            "Value"    => _sortAscending ? rows.OrderBy(r => r.Entry.Value ?? "")     : rows.OrderByDescending(r => r.Entry.Value ?? ""),
            "Type"     => _sortAscending ? rows.OrderBy(r => r.Entry.ValueType ?? "") : rows.OrderByDescending(r => r.Entry.ValueType ?? ""),
            "ReadOnly" => _sortAscending ? rows.OrderBy(r => r.Entry.IsReadOnly)      : rows.OrderByDescending(r => r.Entry.IsReadOnly),
            _          => _sortAscending
                          ? rows.OrderBy(r => CategoryOrder(r.Entry.Category)).ThenBy(r => r.Entry.Name)
                          : rows.OrderByDescending(r => CategoryOrder(r.Entry.Category)).ThenBy(r => r.Entry.Name),
        };

        var grouped = sorted.GroupBy(r => r.Entry.Category ?? "Other")
                            .OrderBy(g => CategoryOrder(g.Key));

        FilteredProperties.Clear();
        foreach (var group in grouped)
        {
            FilteredProperties.Add(new PropertyCategoryGroup(group.Key, group));
        }
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Common"        => 0,
        "Layout"        => 1,
        "Appearance"    => 2,
        "Data Context"  => 3,
        "Visual States" => 4,
        "Style"         => 5,
        "Template"      => 6,
        _               => 7,
    };
}
