using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyGroupViewModel
{
    public PropertyGroupViewModel(string category)
    {
        Category = category;
    }

    public string Category { get; }
    public ObservableCollection<PropertyRowViewModel> Properties { get; } = [];
}
