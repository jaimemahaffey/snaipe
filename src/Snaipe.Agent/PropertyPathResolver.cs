using System.Reflection;
using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Resolves a string property path from a UIElement root to a nested object.
/// The first segment "DataContext" is resolved via DependencyProperty;
/// all other segments use CLR reflection.
/// </summary>
public static class PropertyPathResolver
{
    /// <summary>
    /// Resolves a property path starting from a UI element.
    /// Returns (object, 0, null) on success or (null, errorCode, message) on failure.
    /// </summary>
    public static (object? Value, int ErrorCode, string? Error) Resolve(
        DependencyObject element, string[]? path)
    {
        if (path is null or { Length: 0 })
            return (element, 0, null);

        // "DataContext" as first segment is special — resolved via DependencyProperty.
        object? startObject;
        int startIndex;
        if (path[0] == "DataContext" && element is FrameworkElement fe)
        {
            startObject = fe.DataContext;
            if (startObject is null)
                return (null, ErrorCodes.ElementNotFound, "DataContext is null");
            startIndex = 1;
        }
        else
        {
            startObject = element;
            startIndex = 0;
        }

        return TraversePath(startObject, path, startIndex);
    }

    /// <summary>
    /// Traverses CLR properties on <paramref name="root"/> starting at <paramref name="startIndex"/>.
    /// Exposed as internal for unit testing with plain POCOs.
    /// </summary>
    internal static (object? Value, int ErrorCode, string? Error) TraversePath(
        object root, string[] segments, int startIndex)
    {
        var current = root;
        for (int i = startIndex; i < segments.Length; i++)
        {
            var prop = current.GetType().GetProperty(segments[i],
                BindingFlags.Public | BindingFlags.Instance);
            if (prop is null)
                return (null, ErrorCodes.ElementNotFound,
                    $"Property '{segments[i]}' not found on {current.GetType().Name}");

            var next = prop.GetValue(current);
            if (next is null)
                return (null, ErrorCodes.ElementNotFound,
                    $"Property '{segments[i]}' is null");

            current = next;
        }
        return (current, 0, null);
    }
}
