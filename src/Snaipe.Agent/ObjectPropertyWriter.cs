using System.Reflection;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Writes a single public instance CLR property on an arbitrary object using reflection.
/// Used when the inspector edits a property while drilled into a nested ViewModel.
/// </summary>
public static class ObjectPropertyWriter
{
    public static SetPropertyResult SetProperty(object obj, string propertyName, string newValue)
    {
        var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
            return new SetPropertyResult(false, ErrorCodes.PropertyNotFound,
                "Property not found",
                $"No property '{propertyName}' on {obj.GetType().Name}");

        if (!prop.CanWrite)
            return new SetPropertyResult(false, ErrorCodes.PropertyReadOnly,
                "Property is read-only",
                $"'{propertyName}' does not have a setter.");

        object? parsedValue;
        try
        {
            parsedValue = PropertyWriter.ParseValue(newValue, prop.PropertyType);
        }
        catch (Exception ex)
        {
            return new SetPropertyResult(false, ErrorCodes.InvalidPropertyValue,
                "Invalid property value",
                $"Cannot parse '{newValue}' as {prop.PropertyType.Name}: {ex.Message}");
        }

        prop.SetValue(obj, parsedValue);
        return new SetPropertyResult(true, NormalizedValue: parsedValue?.ToString() ?? newValue);
    }
}
