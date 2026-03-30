// src/Snaipe.Inspector/Controls/PropertyEditorTemplateSelector.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

/// <summary>
/// Selects the right DataTemplate for a PropertyRowViewModel based on ValueKind and IsReadOnly.
/// Templates are set from PropertyGridControl.xaml resources.
/// </summary>
public sealed class PropertyEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ReadOnlyTemplate { get; set; }
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? BooleanTemplate { get; set; }
    public DataTemplate? NumberTemplate { get; set; }
    public DataTemplate? EnumTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is not PropertyRowViewModel row) return TextTemplate;
        if (row.Entry.IsReadOnly) return ReadOnlyTemplate ?? TextTemplate;

        return row.Entry.ValueKind switch
        {
            "Boolean" => BooleanTemplate ?? TextTemplate,
            "Number" => NumberTemplate ?? TextTemplate,
            "Enum" when row.Entry.EnumValues?.Count > 0 => EnumTemplate ?? TextTemplate,
            _ => TextTemplate,
        };
    }
}
