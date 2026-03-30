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

    public PropertyRowViewModel(PropertyEntry entry, Func<PropertyRowViewModel, Task>? commit = null)
    {
        Entry = entry;
        _editValue = entry.Value ?? string.Empty;
        _commit = commit;
        CommitEditCommand = new AsyncRelayCommand(
            () => _commit?.Invoke(this) ?? Task.CompletedTask,
            () => !Entry.IsReadOnly);
    }

    public PropertyEntry Entry { get; }

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
    public Microsoft.UI.Xaml.Media.SolidColorBrush ErrorBorderBrush =>
        HasError
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xBF, 0x00, 0x00))
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00));

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
