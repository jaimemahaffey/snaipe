using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Walks the Uno visual tree and produces <see cref="ElementNode"/> snapshots.
/// </summary>
public static class VisualTreeWalker
{
    public static ElementNode BuildTree(UIElement root)
    {
        return Walk(root);
    }

    private static ElementNode Walk(DependencyObject obj)
    {
        var element = obj as FrameworkElement;
        var node = new ElementNode
        {
            Id = RuntimeHelpers.GetHashCode(obj).ToString(),
            TypeName = obj.GetType().Name,
            Name = element?.Name,
            Bounds = GetBounds(element),
        };

        var childCount = VisualTreeHelper.GetChildrenCount(obj);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            node.Children.Add(Walk(child));
        }

        return node;
    }

    private static BoundsInfo? GetBounds(FrameworkElement? element)
    {
        if (element is null)
            return null;

        return new BoundsInfo
        {
            X = 0, // TODO: transform to root coordinates
            Y = 0,
            Width = element.ActualWidth,
            Height = element.ActualHeight,
        };
    }
}
