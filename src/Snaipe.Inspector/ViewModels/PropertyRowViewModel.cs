using System.Windows.Input;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyRowViewModel : ViewModelBase
{
    private string _editValue;
    private bool _hasError;
    private string? _errorMessage;

    // _commit is set in Task 6 after MainViewModel exists.
    // Declared as nullable; CommitEditCommand is a no-op until wired.
    private readonly Func<PropertyRowViewModel, Task>? _commit;

    public PropertyRowViewModel(PropertyEntry entry,
        Func<PropertyRowViewModel, Task>? commit = null,
        RelayCommand? drillCommand = null,
        RelayCommand? jumpToTemplateCommand = null,
        RelayCommand? showValueChainCommand = null)
    {
        Entry = entry;
        _editValue = entry.Value ?? string.Empty;
        _commit = commit;
        CommitEditCommand = new AsyncRelayCommand(
            () => _commit?.Invoke(this) ?? Task.CompletedTask,
            () => !Entry.IsReadOnly);
        DrillCommand = drillCommand;
        JumpToTemplateCommand = jumpToTemplateCommand;
        ShowValueChainCommand = showValueChainCommand;
        ValueChain = entry.ValueChain?
            .Select(e => new ValueChainEntryViewModel(e))
            .ToArray();
    }

    public PropertyEntry Entry { get; }

    /// <summary>Flat category accessor for AdvancedCollectionView PropertyGroupDescription.</summary>
    public string Category => Entry.Category ?? "Other";

    public string EditValue
    {
        get => _editValue;
        set => SetField(ref _editValue, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetField(ref _hasError, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public AsyncRelayCommand CommitEditCommand { get; }
    public RelayCommand? DrillCommand { get; }
    public RelayCommand? JumpToTemplateCommand { get; }

    public RelayCommand? ShowValueChainCommand { get; }

    /// <summary>
    /// The value chain for this dependency property row, built from protocol data.
    /// Null for synthetic rows (Data Context, Style meta, etc.) and default-only properties.
    /// </summary>
    public IReadOnlyList<ValueChainEntryViewModel>? ValueChain { get; }

    /// <summary>Visibility for the value-chain ? button in the Name column.</summary>
    public Microsoft.UI.Xaml.Visibility ShowValueChainVisibility =>
        ValueChain is { Count: > 0 }
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>Visibility for the jump-to-template button in the Name column.</summary>
    public Microsoft.UI.Xaml.Visibility JumpToTemplateVisibility =>
        JumpToTemplateCommand is not null
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public void SetError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    public void ClearError()
    {
        HasError = false;
        ErrorMessage = null;
    }

    /// <summary>Brush used for red border on text editors when there's an error.</summary>
    public Microsoft.UI.Xaml.Media.SolidColorBrush? ErrorBorderBrush
    {
        get
        {
            if (!HasError) return null;
            try
            {
                return new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xBF, 0x00, 0x00));
            }
            catch
            {
                // DispatcherQueue not available (likely unit tests).
                return null;
            }
        }
    }

    /// <summary>Visibility for the drill-down chevron button in the Name column.</summary>
    public Microsoft.UI.Xaml.Visibility DrillVisibility =>
        Entry.IsObjectValued
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>Two-way bool bridge for CheckBox editor.</summary>
    public bool? IsCheckedValue
    {
        get => bool.TryParse(EditValue, out var b) ? b : null;
        set
        {
            EditValue = value?.ToString() ?? "False";
            CommitEditCommand.Execute(null);
        }
    }

    /// <summary>Two-way double bridge for NumberBox editor.</summary>
    public double NumberValue
    {
        get => double.TryParse(EditValue, System.Globalization.NumberStyles.Any,
                   System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
        set
        {
            EditValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            CommitEditCommand.Execute(null);
        }
    }
}
