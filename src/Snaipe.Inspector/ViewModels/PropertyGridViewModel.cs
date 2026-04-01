using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

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
        SortByCommand = new RelayCommand<string>(SortBy);
        ClearValueChainCommand = new RelayCommand(ClearValueChain);
    }

    public ObservableCollection<PropertyRowViewModel> FilteredProperties { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                RebuildFilteredProperties();
        }
    }

    public string ActiveSortColumn => _activeSortColumn;
    public bool SortAscending => _sortAscending;

    public RelayCommand<string> SortByCommand { get; }
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

    /// <summary>
    /// Shows the value chain panel for <paramref name="row"/>. Calling with the same row
    /// a second time toggles the panel off.
    /// </summary>
    public void ShowValueChain(PropertyRowViewModel row)
    {
        if (row.ValueChain is null) return;

        // Toggle: clicking same row again closes panel
        if (ReferenceEquals(_activeChainRow, row) && ActiveValueChain is not null)
        {
            ClearValueChain();
            return;
        }

        _activeChainRow = row;
        ActiveValueChain = row.ValueChain.ToArray();
        ValueChainPropertyName = $"{row.Entry.Name} — value chain";
    }

    /// <summary>Hides the value chain panel.</summary>
    public void ClearValueChain()
    {
        _activeChainRow = null;
        ActiveValueChain = null;
        ValueChainPropertyName = null;
    }

    public string NameColumnHeader     => "NAME"     + SortIndicator("Name");
    public string TypeColumnHeader     => "TYPE"     + SortIndicator("Type");
    public string CategoryColumnHeader => "CATEGORY" + SortIndicator("Category");
    public string ReadOnlyColumnHeader => "R/O"      + SortIndicator("ReadOnly");

    public void Load(IEnumerable<PropertyRowViewModel> rows)
    {
        _allProperties = rows.ToList();
        RebuildFilteredProperties();
    }

    public void Clear()
    {
        _allProperties = [];
        FilteredProperties.Clear();
        ClearValueChain();
    }

    private void SortBy(string? column)
    {
        if (column is null) return;
        if (_activeSortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _activeSortColumn = column;
            _sortAscending = true;
        }
        OnPropertyChanged(nameof(ActiveSortColumn));
        OnPropertyChanged(nameof(SortAscending));
        OnPropertyChanged(nameof(NameColumnHeader));
        OnPropertyChanged(nameof(TypeColumnHeader));
        OnPropertyChanged(nameof(CategoryColumnHeader));
        OnPropertyChanged(nameof(ReadOnlyColumnHeader));
        RebuildFilteredProperties();
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

        FilteredProperties.Clear();
        foreach (var row in sorted)
            FilteredProperties.Add(row);
    }

    private string SortIndicator(string column)
    {
        if (_activeSortColumn != column) return "";
        return _sortAscending ? " ↑" : " ↓";
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
