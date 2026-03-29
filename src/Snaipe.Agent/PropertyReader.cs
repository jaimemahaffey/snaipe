using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Snaipe.Agent;

/// <summary>
/// Reads dependency properties from a UIElement using reflection, with per-type caching.
/// </summary>
public static class PropertyReader
{
    private static readonly ConcurrentDictionary<Type, List<DependencyPropertyInfo>> PropertyCache = new();

    /// <summary>
    /// Get all dependency properties for the given element, categorized and formatted.
    /// Must be called on the UI thread.
    /// </summary>
    public static List<Protocol.PropertyEntry> GetProperties(DependencyObject element)
    {
        var props = GetDependencyProperties(element.GetType());
        var entries = new List<Protocol.PropertyEntry>(props.Count + 8);

        foreach (var dpInfo in props)
        {
            try
            {
                var value = element.GetValue(dpInfo.Property);
                var localValue = element.ReadLocalValue(dpInfo.Property);

                string? bindingExpression = null;
                if (localValue is BindingExpression be)
                {
                    bindingExpression = FormatBindingExpression(be);
                }

                var valueType = dpInfo.PropertyType;
                var isReadOnly = dpInfo.IsReadOnly;

                // Unwrap Nullable<T> for enum name resolution (matches GetValueKind behaviour).
                var effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;

                entries.Add(new Protocol.PropertyEntry
                {
                    Name = dpInfo.Name,
                    Category = CategorizeProperty(dpInfo.Name),
                    ValueType = valueType.Name,
                    Value = FormatValue(value),
                    ValueKind = GetValueKind(valueType),
                    IsReadOnly = isReadOnly,
                    BindingExpression = bindingExpression,
                    EnumValues = effectiveType.IsEnum ? Enum.GetNames(effectiveType).ToList() : null,
                });
            }
            catch
            {
                // Skip properties that throw on read (can happen for uninitialized elements).
            }
        }

        entries.AddRange(GetAttachedProperties(element));

        return entries;
    }

    private static List<DependencyPropertyInfo> GetDependencyProperties(Type type)
    {
        return PropertyCache.GetOrAdd(type, t =>
        {
            var result = new List<DependencyPropertyInfo>();
            var current = t;

            while (current != null && current != typeof(object))
            {
                var fields = current.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(DependencyProperty) &&
                        field.Name.EndsWith("Property", StringComparison.Ordinal))
                    {
                        if (field.GetValue(null) is DependencyProperty dp)
                        {
                            var propertyName = field.Name[..^"Property".Length];

                            // Try to find the CLR property to determine the type and read-only status.
                            var clrProperty = t.GetProperty(propertyName,
                                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                            var propertyType = clrProperty?.PropertyType ?? typeof(object);
                            var isReadOnly = clrProperty is null || !clrProperty.CanWrite;

                            result.Add(new DependencyPropertyInfo(propertyName, dp, propertyType, isReadOnly));
                        }
                    }
                }

                current = current.BaseType;
            }

            // Deduplicate (base class fields may appear via FlattenHierarchy)
            return result
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .OrderBy(p => p.Name)
                .ToList();
        });
    }

    private static string CategorizeProperty(string name)
    {
        return name switch
        {
            "Width" or "Height" or "MinWidth" or "MinHeight" or "MaxWidth" or "MaxHeight"
                or "Margin" or "Padding" or "HorizontalAlignment" or "VerticalAlignment"
                or "HorizontalContentAlignment" or "VerticalContentAlignment"
                or "ActualWidth" or "ActualHeight" => "Layout",

            "Background" or "Foreground" or "Opacity" or "Visibility"
                or "BorderBrush" or "BorderThickness" or "CornerRadius"
                or "FontSize" or "FontWeight" or "FontFamily" or "FontStyle" => "Appearance",

            "Name" or "DataContext" or "Tag" or "Content" or "Text"
                or "Header" or "PlaceholderText" or "IsEnabled"
                or "IsChecked" or "Value" or "SelectedItem" => "Common",

            _ => "Other"
        };
    }

    private static string GetValueKind(Type type)
    {
        // Unwrap Nullable<T> so e.g. bool? reports as "Boolean" rather than "Object".
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        if (type == typeof(string)) return "String";
        if (type == typeof(bool)) return "Boolean";
        if (type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(long))
            return "Number";
        if (type == typeof(Windows.UI.Color) || type == typeof(SolidColorBrush)) return "Color";
        if (type == typeof(Thickness)) return "Thickness";
        if (type.IsEnum) return "Enum";
        return "Object";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            double d when double.IsNaN(d) => "NaN",
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f when float.IsNaN(f) => "NaN",
            float f => f.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            bool b => b.ToString(),
            Windows.UI.Color c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
            SolidColorBrush brush => $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}",
            Thickness t => $"{t.Left.ToString(CultureInfo.InvariantCulture)},{t.Top.ToString(CultureInfo.InvariantCulture)},{t.Right.ToString(CultureInfo.InvariantCulture)},{t.Bottom.ToString(CultureInfo.InvariantCulture)}",
            Enum e => e.ToString(),
            _ => value.ToString() ?? "(null)"
        };
    }

    /// <summary>
    /// Enumerates well-known attached properties from Grid and Canvas that have been explicitly
    /// set on <paramref name="element"/>. Only properties with local values are included so
    /// that the Inspector does not show noise for every element in the tree.
    /// </summary>
    private static List<Protocol.PropertyEntry> GetAttachedProperties(DependencyObject element)
    {
        var results = new List<Protocol.PropertyEntry>();

        // Table of (owner type name, property field name, display name, expected CLR type) tuples.
        var candidates = new (string OwnerTypeName, string FieldName, string DisplayName, Type ClrType)[]
        {
            ("Grid",   "RowProperty",        "Grid.Row",       typeof(int)),
            ("Grid",   "ColumnProperty",     "Grid.Column",    typeof(int)),
            ("Grid",   "RowSpanProperty",    "Grid.RowSpan",   typeof(int)),
            ("Grid",   "ColumnSpanProperty", "Grid.ColumnSpan",typeof(int)),
            ("Canvas", "LeftProperty",       "Canvas.Left",    typeof(double)),
            ("Canvas", "TopProperty",        "Canvas.Top",     typeof(double)),
            ("Canvas", "ZIndexProperty",     "Canvas.ZIndex",  typeof(int)),
        };

        foreach (var (ownerTypeName, fieldName, displayName, clrType) in candidates)
        {
            try
            {
                var ownerType = Type.GetType(
                    $"Microsoft.UI.Xaml.Controls.{ownerTypeName}, Microsoft.WinUI")
                    ?? AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try { return a.GetTypes(); }
                            catch { return []; }
                        })
                        .FirstOrDefault(t => t.Name == ownerTypeName &&
                                             t.Namespace == "Microsoft.UI.Xaml.Controls");

                if (ownerType is null) continue;

                var field = ownerType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
                if (field?.GetValue(null) is not DependencyProperty dp) continue;

                if (element.ReadLocalValue(dp) == DependencyProperty.UnsetValue) continue;

                var value = element.GetValue(dp);

                results.Add(new Protocol.PropertyEntry
                {
                    Name = displayName,
                    Category = "Layout",
                    ValueType = clrType.Name,
                    Value = FormatValue(value),
                    ValueKind = GetValueKind(clrType),
                    IsReadOnly = false,
                });
            }
            catch
            {
                // Attached property owner type not available in this process — skip.
            }
        }

        return results;
    }

    /// <summary>
    /// Formats a <see cref="BindingExpression"/> as a Binding markup-extension string,
    /// including Mode and ElementName when they differ from their defaults.
    /// </summary>
    private static string FormatBindingExpression(BindingExpression be)
    {
        var binding = be.ParentBinding;
        if (binding is null) return "{Binding}";

        var path = binding.Path?.Path;
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(path))
            parts.Add($"Path={path}");

        // Only include Mode when it has been explicitly set to a non-default value.
        if (binding.Mode != BindingMode.OneWay)
            parts.Add($"Mode={binding.Mode}");

        // ElementName is rarely set; include it when present to make the binding self-documenting.
        var elementName = binding.ElementName as string;
        if (!string.IsNullOrEmpty(elementName))
            parts.Add($"ElementName={elementName}");

        return parts.Count == 0 ? "{Binding}" : $"{{Binding {string.Join(", ", parts)}}}";
    }

    private record struct DependencyPropertyInfo(
        string Name,
        DependencyProperty Property,
        Type PropertyType,
        bool IsReadOnly);
}
