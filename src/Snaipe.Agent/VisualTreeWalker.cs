using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
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
        return Walk(root, parent: null, root, tracker, depth: 0);
    }

    /// <summary>
    /// Build a snapshot of the visual tree including open popup subtrees.
    /// Returns a list where element [0] is the Window.Content subtree and
    /// subsequent elements are open popups (TypeName = "[Popup]").
    /// </summary>
    public static List<ElementNode> BuildTree(
        UIElement windowContent, XamlRoot xamlRoot, ElementTracker tracker)
    {
        var roots = new List<ElementNode>
        {
            Walk(windowContent, parent: null, windowContent, tracker, depth: 0)
        };

        try
        {
            var openPopups = VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot);
            foreach (var popup in openPopups)
            {
                if (popup.Child is not UIElement popupChild) continue;

                var popupRoot = new ElementNode
                {
                    Id = tracker.GetOrAssignId(popup.Child),
                    TypeName = "[Popup]",
                    Name = popup.Name is { Length: > 0 } ? popup.Name : null,
                    Children = { Walk(popupChild, parent: null, popupChild, tracker, depth: 0) },
                };
                roots.Add(popupRoot);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VisualTreeWalker] Popup enumeration failed: {ex.Message}");
        }

        return roots;
    }

    /// <summary>
    /// Legacy overload — builds tree without an ElementTracker (uses hash-based IDs).
    /// </summary>
    public static ElementNode BuildTree(UIElement root)
    {
        return WalkLegacy(root);
    }

    private static ElementNode Walk(DependencyObject obj, DependencyObject? parent,
        UIElement treeRoot, ElementTracker tracker, int depth)
    {
        var element = obj as UIElement;
        var fe = obj as FrameworkElement;

        var id = element is not null
            ? tracker.GetOrAssignId(element)
            : RuntimeHelpers.GetHashCode(obj).ToString();

        // Update reverse lookup for quick ID→element resolution.
        if (element is not null)
            tracker.UpdateReverseLookup(element);

        var (templateOrigin, templateInstanceCount) = DetectTemplateOrigin(obj, parent);

        var node = new ElementNode
        {
            Id = id,
            TypeName = obj.GetType().Name,
            Name = fe?.Name,
            // Bounds deferred for performance — computed on GetProperties or Highlight.
            Bounds = null,
            TemplateOrigin = templateOrigin,
            TemplateInstanceCount = templateInstanceCount,
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
            node.Children.Add(Walk(child, obj, treeRoot, tracker, depth + 1));
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

    /// <summary>
    /// Determines whether <paramref name="child"/> is the root of an instantiated template.
    /// Returns (origin, instanceCount) or (null, null) if not a template root.
    /// </summary>
    private static (string? Origin, int? InstanceCount) DetectTemplateOrigin(
        DependencyObject child, DependencyObject? parent)
    {
        if (parent is null) return (null, null);

        // ControlTemplate root: first visual child of a Control that has a Template set.
        if (parent is Control ctrl && ctrl.Template is not null)
        {
            if (VisualTreeHelper.GetChildrenCount(parent) > 0 &&
                VisualTreeHelper.GetChild(parent, 0) == child)
                return ("ControlTemplate", null);
        }

        // ContentTemplate / ItemTemplate root: first visual child of a ContentPresenter
        // that has a ContentTemplate set.
        if (parent is ContentPresenter cp && cp.ContentTemplate is not null)
        {
            if (VisualTreeHelper.GetChildrenCount(parent) > 0 &&
                VisualTreeHelper.GetChild(parent, 0) == child)
            {
                // Walk up the visual parent chain (max 5 levels) to detect a SelectorItem
                // ancestor, which indicates we are inside an item container (ItemTemplate).
                DependencyObject? ancestor = VisualTreeHelper.GetParent(parent);
                for (var i = 0; i < 5 && ancestor is not null; i++)
                {
                    if (ancestor is SelectorItem)
                    {
                        var itemsControl = FindAncestorItemsControl(ancestor);
                        var count = itemsControl is not null
                            ? CountSelectorItems(itemsControl)
                            : 1;
                        return ("ItemTemplate", count);
                    }
                    ancestor = VisualTreeHelper.GetParent(ancestor);
                }
                return ("ContentTemplate", null);
            }
        }

        return (null, null);
    }

    private static ItemsControl? FindAncestorItemsControl(DependencyObject node)
    {
        var current = VisualTreeHelper.GetParent(node);
        while (current is not null)
        {
            if (current is ItemsControl ic) return ic;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static int CountSelectorItems(DependencyObject root)
    {
        var count = 0;
        CountSelectorItemsRecursive(root, ref count);
        return count;
    }

    private static void CountSelectorItemsRecursive(DependencyObject node, ref int count)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is SelectorItem) count++;
            CountSelectorItemsRecursive(child, ref count);
        }
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
