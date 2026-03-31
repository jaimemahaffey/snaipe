using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Snaipe.Agent;

/// <summary>
/// Result of a property set operation.
/// </summary>
public record struct SetPropertyResult(bool Success, int ErrorCode = 0, string? Error = null, string? Details = null, string? NormalizedValue = null);

/// <summary>
/// Writes dependency property values on a UIElement by parsing string values.
/// Must be called on the UI thread.
/// </summary>
public static class PropertyWriter
{
    /// <summary>
    /// Set a dependency property on an element by name, parsing the string value
    /// into the appropriate CLR type.
    /// </summary>
    /// <returns>A SetPropertyResult indicating success or failure.</returns>
    public static SetPropertyResult SetProperty(
        DependencyObject element, string propertyName, string newValue)
    {
        // Find the DependencyProperty by name.
        var dpField = FindDependencyPropertyField(element.GetType(), propertyName);
        if (dpField is null)
        {
            return new SetPropertyResult(false, Protocol.ErrorCodes.PropertyNotFound,
                "Property not found",
                $"No DependencyProperty '{propertyName}' on {element.GetType().Name}");
        }

        var dp = (DependencyProperty)dpField.GetValue(null)!;

        // Check if the property is read-only by looking for a CLR setter.
        var clrProperty = element.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (clrProperty is not null && !clrProperty.CanWrite)
        {
            return new SetPropertyResult(false, Protocol.ErrorCodes.PropertyReadOnly,
                "Property is read-only",
                $"'{propertyName}' does not have a setter.");
        }

        // Parse the new value.
        var targetType = clrProperty?.PropertyType ?? typeof(object);
        object? parsedValue;
        try
        {
            parsedValue = ParseValue(newValue, targetType);
        }
        catch (Exception ex)
        {
            return new SetPropertyResult(false, Protocol.ErrorCodes.InvalidPropertyValue,
                "Invalid property value",
                $"Cannot parse '{newValue}' as {targetType.Name}: {ex.Message}");
        }

        // Set the value.
        element.SetValue(dp, parsedValue);
        return new SetPropertyResult(true, NormalizedValue: parsedValue?.ToString() ?? newValue);
    }

    private static FieldInfo? FindDependencyPropertyField(Type type, string propertyName)
    {
        var fieldName = propertyName + "Property";
        var current = type;
        while (current != null && current != typeof(object))
        {
            var field = current.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field is not null && field.FieldType == typeof(DependencyProperty))
                return field;
            current = current.BaseType;
        }
        return null;
    }

    internal static object? ParseValue(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType == typeof(bool))
            return bool.Parse(value);

        if (targetType == typeof(double))
            return double.Parse(value, CultureInfo.InvariantCulture);

        if (targetType == typeof(float))
            return float.Parse(value, CultureInfo.InvariantCulture);

        if (targetType == typeof(int))
            return int.Parse(value, CultureInfo.InvariantCulture);

        if (targetType == typeof(long))
            return long.Parse(value, CultureInfo.InvariantCulture);

        if (targetType == typeof(Windows.UI.Color))
            return ParseColor(value);

        if (targetType == typeof(SolidColorBrush))
            return new SolidColorBrush(ParseColor(value));

        if (targetType == typeof(Thickness))
            return ParseThickness(value);

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        // Fallback: try TypeDescriptor
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
            return converter.ConvertFromInvariantString(value);

        throw new InvalidOperationException($"No parser available for type '{targetType.Name}'.");
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        // Expect #AARRGGBB or #RRGGBB
        hex = hex.TrimStart('#');
        return hex.Length switch
        {
            8 => Windows.UI.Color.FromArgb(
                byte.Parse(hex[..2], NumberStyles.HexNumber),
                byte.Parse(hex[2..4], NumberStyles.HexNumber),
                byte.Parse(hex[4..6], NumberStyles.HexNumber),
                byte.Parse(hex[6..8], NumberStyles.HexNumber)),
            6 => Windows.UI.Color.FromArgb(
                0xFF,
                byte.Parse(hex[..2], NumberStyles.HexNumber),
                byte.Parse(hex[2..4], NumberStyles.HexNumber),
                byte.Parse(hex[4..6], NumberStyles.HexNumber)),
            _ => throw new FormatException($"Invalid color format: #{hex}")
        };
    }

    private static Thickness ParseThickness(string value)
    {
        var parts = value.Split(',');
        return parts.Length switch
        {
            1 => new Thickness(double.Parse(parts[0], CultureInfo.InvariantCulture)),
            2 => new Thickness(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)),
            4 => new Thickness(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(parts[2], CultureInfo.InvariantCulture),
                double.Parse(parts[3], CultureInfo.InvariantCulture)),
            _ => throw new FormatException($"Invalid thickness format: {value}")
        };
    }
}
