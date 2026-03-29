using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Walks the Uno visual tree and produces <see cref="ElementNode"/> snapshots.
/// Uses <see cref="ElementTracker"/> for stable element IDs.
/// </summary>
public static class VisualTreeWalker
{
    /// <summary>Maximum tree depth to prevent stack overflow on pathological trees.</summary>
    private const int MaxDepth = 100;

    /// <summary>Maximum children per node to prevent excessive serialization.</summary>
    private const int MaxChildrenPerNode = 2000;

    /// <summary>
    /// Build a snapshot of the visual tree rooted at <paramref name="root"/>.
    /// Bounds are deferred (set to null) for performance — they are computed
    /// lazily when properties or highlights are requested.
    /// </summary>
    public static ElementNode BuildTree(UIElement root, ElementTracker tracker)
    {
        return Walk(root, root, tracker, depth: 0);
    }

    /// <summary>
    /// Legacy overload — builds tree without an ElementTracker (uses hash-based IDs).
    /// </summary>
    public static ElementNode BuildTree(UIElement root)
    {
        return WalkLegacy(root);
    }

    private static ElementNode Walk(DependencyObject obj, UIElement treeRoot, ElementTracker tracker, int depth)
    {
        var element = obj as UIElement;
        var fe = obj as FrameworkElement;

        var id = element is not null
            ? tracker.GetOrAssignId(element)
            : RuntimeHelpers.GetHashCode(obj).ToString();

        // Update reverse lookup for quick ID→element resolution.
        if (element is not null)
            tracker.UpdateReverseLookup(element);

        var node = new ElementNode
        {
            Id = id,
            TypeName = obj.GetType().Name,
            Name = fe?.Name,
            // Bounds deferred for performance — computed on GetProperties or Highlight.
            Bounds = null,
        };

        if (depth >= MaxDepth)
        {
            var childCount = VisualTreeHelper.GetChildrenCount(obj);
            if (childCount > 0)
            {
                node.Children.Add(new ElementNode
                {
                    Id = $"truncated-depth-{depth}",
                    TypeName = $"[Truncated: {childCount} children omitted — max depth {MaxDepth}]",
                });
            }
            return node;
        }

        var totalChildren = VisualTreeHelper.GetChildrenCount(obj);
        var limit = Math.Min(totalChildren, MaxChildrenPerNode);

        for (var i = 0; i < limit; i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            node.Children.Add(Walk(child, treeRoot, tracker, depth + 1));
        }

        if (totalChildren > MaxChildrenPerNode)
        {
            node.Children.Add(new ElementNode
            {
                Id = $"truncated-children-{id}",
                TypeName = $"[Truncated: {totalChildren - MaxChildrenPerNode} children omitted — max {MaxChildrenPerNode}]",
            });
        }

        return node;
    }

    private static ElementNode WalkLegacy(DependencyObject obj)
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
            node.Children.Add(WalkLegacy(child));
        }

        return node;
    }

    /// <summary>
    /// Compute bounds for a specific element relative to a root element.
    /// Called on-demand for property inspection or highlighting.
    /// </summary>
    public static BoundsInfo GetBoundsRelativeTo(UIElement element, UIElement root)
    {
        try
        {
            var transform = element.TransformToVisual(root);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            var fe = element as FrameworkElement;

            return new BoundsInfo
            {
                X = position.X,
                Y = position.Y,
                Width = fe?.ActualWidth ?? 0,
                Height = fe?.ActualHeight ?? 0,
            };
        }
        catch
        {
            return new BoundsInfo { X = 0, Y = 0, Width = 0, Height = 0 };
        }
    }

    private static BoundsInfo? GetBounds(FrameworkElement? element)
    {
        if (element is null)
            return null;

        return new BoundsInfo
        {
            X = 0,
            Y = 0,
            Width = element.ActualWidth,
            Height = element.ActualHeight,
        };
    }
}
