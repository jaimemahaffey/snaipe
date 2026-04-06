using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

public class PropertyCategoryGroup : ObservableCollection<PropertyRowViewModel>, IGrouping<string, PropertyRowViewModel>
{
    public string Key { get; }
    public PropertyCategoryGroup(string key, IEnumerable<PropertyRowViewModel> items) : base(items)
    {
        Key = key;
    }

    public override string ToString() => Key;
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

    private ObservableCollection<PropertyCategoryGroup> _filteredProperties = [];
    public ObservableCollection<PropertyCategoryGroup> FilteredProperties
    {
        get => _filteredProperties;
        private set => SetField(ref _filteredProperties, value);
    }

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

    /// <summary>
    /// Fires after <see cref="FilteredProperties"/> has been fully rebuilt or cleared,
    /// so the view layer can reassign the CollectionViewSource exactly once per logical update.
    /// </summary>
    public event EventHandler? PropertiesRebuilt;

    public void Load(IEnumerable<PropertyRowViewModel> rows)
    {
        _allProperties = rows.ToList();
        RebuildFilteredProperties();
    }

    public void Clear()
    {
        _allProperties.Clear();
        FilteredProperties = [];
        ClearValueChain();
        PropertiesRebuilt?.Invoke(this, EventArgs.Empty);
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

        var newCollection = new ObservableCollection<PropertyCategoryGroup>();
        foreach (var group in grouped)
            newCollection.Add(new PropertyCategoryGroup(group.Key, group));
        FilteredProperties = newCollection;
        PropertiesRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private static int CategoryOrder(string? category) => category switch
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
