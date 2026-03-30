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
}
