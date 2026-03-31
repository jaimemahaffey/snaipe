using System.Reflection;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Reads all public instance CLR properties from an arbitrary object using reflection.
/// Used when the inspector drills into a nested ViewModel or DataContext.
/// </summary>
public static class ObjectPropertyReader
{
    public static List<PropertyEntry> GetProperties(object obj)
    {
        var results = new List<PropertyEntry>();
        var props = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            try
            {
                var value = prop.GetValue(obj);
                var effectiveType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var kind = PropertyReader.GetValueKind(prop.PropertyType);

                results.Add(new PropertyEntry
                {
                    Name           = prop.Name,
                    Category       = "Properties",
                    ValueType      = prop.PropertyType.Name,
                    Value          = PropertyReader.FormatValue(value),
                    ValueKind      = kind,
                    IsReadOnly     = !prop.CanWrite,
                    IsObjectValued = kind == "Object" && value is not null,
                    EnumValues     = effectiveType.IsEnum
                        ? Enum.GetNames(effectiveType).ToList()
                        : null,
                });
            }
            catch
            {
                // Skip properties whose getter throws.
            }
        }

        return results;
    }
}
