# DataTemplate Visualization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Annotate template-root nodes in the visual tree with badges, and add one-click navigation from a template property row in the Inspector property grid to the corresponding template root node in the tree.

**Architecture:** Tree-walk time detection (`DetectTemplateOrigin`) annotates each `ElementNode` with a `TemplateOrigin` string. The Inspector holds the full tree in memory and performs a client-side DFS to locate the target node on click — no new IPC messages required.

**Tech Stack:** C# 13, .NET 9, Uno Platform 6.5 (WinUI), xUnit

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Protocol/ElementNode.cs` | Modify — add `TemplateOrigin`, `TemplateInstanceCount` to `ElementNode`; add `TemplateOriginKind` to `PropertyEntry` |
| `src/Snaipe.Agent/VisualTreeWalker.cs` | Modify — add parent parameter to `Walk`, add `DetectTemplateOrigin` |
| `src/Snaipe.Agent/PropertyReader.cs` | Modify — set `TemplateOriginKind` on template property rows in `GetTemplateEntries` |
| `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs` | Modify — add `TemplateLabel`, `TemplateLabelVisibility` |
| `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs` | Modify — add `JumpToTemplateCommand`, `JumpToTemplateVisibility`; constructor gains optional `jumpToTemplateCommand` |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Modify — add `ScrollIntoViewRequested` event, `JumpToTemplateRoot`, `FindTemplateRoot`; wire `jumpToTemplateCommand` in `LoadPropertiesAsync` |
| `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml` | Modify — add template badge chip to tree node DataTemplate |
| `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs` | Modify — add `_nodeMap`, make `BuildNode` instance method, subscribe to `ScrollIntoViewRequested` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | Modify — add jump-arrow button alongside drill chevron |
| `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs` | Modify — add `TemplateLabel` / `TemplateLabelVisibility` tests |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | Modify — add `JumpToTemplateRoot` tests |
| `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` | Modify — add `JumpToTemplateCommand` tests |

---

## Task 1: Protocol — TemplateOrigin fields

**Files:**
- Modify: `src/Snaipe.Protocol/ElementNode.cs`

- [x] **Step 1: Add `TemplateOrigin` and `TemplateInstanceCount` to `ElementNode`, and `TemplateOriginKind` to `PropertyEntry`**

Replace the contents of `src/Snaipe.Protocol/ElementNode.cs` with:

```csharp
namespace Snaipe.Protocol;

/// <summary>
/// Represents a single element in the visual tree.
/// </summary>
public sealed class ElementNode
{
    public required string Id { get; init; }
    public required string TypeName { get; init; }
    public string? Name { get; init; }
    public List<PropertyEntry> Properties { get; init; } = [];
    public List<ElementNode> Children { get; init; } = [];
    public BoundsInfo? Bounds { get; init; }

    /// <summary>
    /// Set when this element is the root of an instantiated template.
    /// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
    /// </summary>
    public string? TemplateOrigin { get; init; }

    /// <summary>
    /// For ItemTemplate roots: number of realized item containers currently in the visual tree.
    /// Null for all other template kinds.
    /// </summary>
    public int? TemplateInstanceCount { get; init; }
}

public sealed class BoundsInfo
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

public sealed class PropertyEntry
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string? ValueType { get; init; }
    public string? Value { get; init; }
    /// <summary>
    /// Hint for Inspector editors: "String", "Number", "Boolean", "Color", "Thickness", "Enum", "Object".
    /// </summary>
    public string ValueKind { get; init; } = "Object";
    public bool IsReadOnly { get; init; }
    public string? BindingExpression { get; init; }
    /// <summary>
    /// Populated when ValueKind is "Enum". Contains all valid enum member names so the Inspector
    /// can render a ComboBox instead of a free-text field.
    /// </summary>
    public List<string>? EnumValues { get; init; }
    /// <summary>
    /// True when ValueKind is "Object" and the value is non-null.
    /// Inspector renders a drill-down chevron for these rows.
    /// </summary>
    public bool IsObjectValued { get; init; }
    /// <summary>
    /// When set, this property row is a template navigation target.
    /// The Inspector renders a jump arrow and on click performs a client-side
    /// DFS search for a descendant node with matching TemplateOrigin.
    /// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
    /// </summary>
    public string? TemplateOriginKind { get; init; }
}
```

- [x] **Step 2: Build to verify no compile errors**

```bash
dotnet build src/Snaipe.Protocol/Snaipe.Protocol.csproj -v quiet
```

Expected: Build succeeded, 0 error(s).

- [x] **Step 3: Commit**

```bash
git add src/Snaipe.Protocol/ElementNode.cs
git commit -m "feat: add TemplateOrigin, TemplateInstanceCount to ElementNode; TemplateOriginKind to PropertyEntry"
```

---

## Task 2: VisualTreeWalker — detect template roots at walk time

**Files:**
- Modify: `src/Snaipe.Agent/VisualTreeWalker.cs`

- [x] **Step 1: Add parent threading and DetectTemplateOrigin to VisualTreeWalker**

Replace the full file `src/Snaipe.Agent/VisualTreeWalker.cs` with:

```csharp
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
```

- [x] **Step 2: Build Agent project to verify no compile errors**

```bash
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet
```

Expected: Build succeeded, 0 error(s).

- [x] **Step 3: Commit**

```bash
git add src/Snaipe.Agent/VisualTreeWalker.cs
git commit -m "feat: thread parent through Walk, detect ControlTemplate/ContentTemplate/ItemTemplate roots"
```

---

## Task 3: PropertyReader — tag template property rows

**Files:**
- Modify: `src/Snaipe.Agent/PropertyReader.cs`

The existing `GetTemplateEntries` builds property rows for `ControlTemplate`, `ContentTemplate`, and `ItemTemplate`. Each row needs `TemplateOriginKind` set so the Inspector knows to render a jump arrow.

- [x] **Step 1: Set `TemplateOriginKind` on each template row in `GetTemplateEntries`**

Find the `GetTemplateEntries` method (around line 495 in `PropertyReader.cs`). Replace the three `results.Add(...)` calls with the versions below that include `TemplateOriginKind`:

```csharp
    private static List<Protocol.PropertyEntry> GetTemplateEntries(DependencyObject element)
    {
        var results = new List<Protocol.PropertyEntry>();

        if (element is Control ctrl && ctrl.Template is not null)
        {
            results.Add(new Protocol.PropertyEntry
            {
                Name = "ControlTemplate",
                Category = "Template",
                ValueType = "ControlTemplate",
                Value = ctrl.Template.TargetType?.Name ?? "(set)",
                ValueKind = "String",
                IsReadOnly = true,
                TemplateOriginKind = "ControlTemplate",
            });
        }

        if (element is ContentPresenter cp && cp.ContentTemplate is not null)
        {
            string? rootType = null;
            if (VisualTreeHelper.GetChildrenCount(cp) > 0)
            {
                var child = VisualTreeHelper.GetChild(cp, 0);
                rootType = child.GetType().Name;
            }

            results.Add(new Protocol.PropertyEntry
            {
                Name = "ContentTemplate",
                Category = "Template",
                ValueType = "DataTemplate",
                Value = rootType is not null ? $"Root: {rootType}" : "(set)",
                ValueKind = "String",
                IsReadOnly = true,
                TemplateOriginKind = "ContentTemplate",
            });
        }

        if (element is ItemsControl ic && ic.ItemTemplate is not null)
        {
            results.Add(new Protocol.PropertyEntry
            {
                Name = "ItemTemplate",
                Category = "Template",
                ValueType = "DataTemplate",
                Value = "(set)",
                ValueKind = "String",
                IsReadOnly = true,
                TemplateOriginKind = "ItemTemplate",
            });
        }

        return results;
    }
```

- [x] **Step 2: Build Agent project**

```bash
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet
```

Expected: Build succeeded, 0 error(s).

- [x] **Step 3: Commit**

```bash
git add src/Snaipe.Agent/PropertyReader.cs
git commit -m "feat: set TemplateOriginKind on template property rows in GetTemplateEntries"
```

---

## Task 4: TreeNodeViewModel — TemplateLabel + tests

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs`
- Modify: `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs`

- [x] **Step 1: Write the failing tests**

Add these test methods to `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs` (append inside the class before the closing `}`):

```csharp
    [Fact]
    public void TemplateLabel_ControlTemplate_ReturnsOriginString()
    {
        var node = MakeNode("1", "Border");
        node = node with { TemplateOrigin = "ControlTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ControlTemplate", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_ItemTemplateWithCount_ReturnsCountSuffix()
    {
        var node = MakeNode("1", "Grid");
        node = node with { TemplateOrigin = "ItemTemplate", TemplateInstanceCount = 5 };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ItemTemplate ×5", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_Null_ReturnsNull()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Null(vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabelVisibility_NullOrigin_IsCollapsed()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.TemplateLabelVisibility);
    }

    [Fact]
    public void TemplateLabelVisibility_WithOrigin_IsVisible()
    {
        var node = MakeNode("1", "Border");
        node = node with { TemplateOrigin = "ContentTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.TemplateLabelVisibility);
    }
```

Note: the `MakeNode` helper in that file returns `ElementNode`. The `with` expression works because `ElementNode` is a `sealed class` — but `with` only works on records and structs. Change the test helper to directly construct `ElementNode` with the needed fields instead:

Replace the five test methods above with these (using constructor syntax instead of `with`):

```csharp
    [Fact]
    public void TemplateLabel_ControlTemplate_ReturnsOriginString()
    {
        var node = new ElementNode { Id = "1", TypeName = "Border", TemplateOrigin = "ControlTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ControlTemplate", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_ItemTemplateWithCount_ReturnsCountSuffix()
    {
        var node = new ElementNode { Id = "1", TypeName = "Grid", TemplateOrigin = "ItemTemplate", TemplateInstanceCount = 5 };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ItemTemplate ×5", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_Null_ReturnsNull()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Null(vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabelVisibility_NullOrigin_IsCollapsed()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.TemplateLabelVisibility);
    }

    [Fact]
    public void TemplateLabelVisibility_WithOrigin_IsVisible()
    {
        var node = new ElementNode { Id = "1", TypeName = "Border", TemplateOrigin = "ContentTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.TemplateLabelVisibility);
    }
```

- [x] **Step 2: Run the failing tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~TemplateLabel"
```

Expected: 5 tests FAIL with errors about missing `TemplateLabel` / `TemplateLabelVisibility` members.

- [x] **Step 3: Add TemplateLabel and TemplateLabelVisibility to TreeNodeViewModel**

In `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs`, add these two properties after the `IsSelected` property (before the closing `}`):

```csharp
    public string? TemplateLabel =>
        Node.TemplateOrigin switch
        {
            "ItemTemplate" when Node.TemplateInstanceCount is { } n => $"ItemTemplate ×{n}",
            { } origin => origin,
            _ => null
        };

    public Microsoft.UI.Xaml.Visibility TemplateLabelVisibility =>
        TemplateLabel is not null
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
```

- [x] **Step 4: Run the tests and verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~TemplateLabel"
```

Expected: 5 tests PASS.

- [x] **Step 5: Run the full Inspector test suite to check for regressions**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: All tests PASS.

- [x] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs
git commit -m "feat: add TemplateLabel, TemplateLabelVisibility to TreeNodeViewModel"
```

---

## Task 5: PropertyRowViewModel — JumpToTemplateCommand + tests

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`
- Modify: `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs`

- [x] **Step 1: Write the failing tests**

Append these two test methods to `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` (inside the class before the closing `}`):

```csharp
    [Fact]
    public void JumpToTemplateCommand_WhenSet_IsNonNull()
    {
        var entry = new PropertyEntry
        {
            Name = "ControlTemplate", Category = "Template",
            ValueKind = "String", TemplateOriginKind = "ControlTemplate"
        };
        var cmd = new RelayCommand(() => { });
        var vm = new PropertyRowViewModel(entry, jumpToTemplateCommand: cmd);
        Assert.NotNull(vm.JumpToTemplateCommand);
    }

    [Fact]
    public void JumpToTemplateCommand_WhenNotSet_IsNull()
    {
        var entry = new PropertyEntry { Name = "Width", Category = "Layout", ValueKind = "Number" };
        var vm = new PropertyRowViewModel(entry);
        Assert.Null(vm.JumpToTemplateCommand);
    }
```

- [x] **Step 2: Run the failing tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~JumpToTemplateCommand"
```

Expected: 2 tests FAIL — `JumpToTemplateCommand` does not exist on `PropertyRowViewModel`.

- [x] **Step 3: Add JumpToTemplateCommand and JumpToTemplateVisibility to PropertyRowViewModel**

In `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`:

1. Add `RelayCommand? jumpToTemplateCommand = null` as the last parameter to the constructor:

```csharp
    public PropertyRowViewModel(PropertyEntry entry,
        Func<PropertyRowViewModel, Task>? commit = null,
        RelayCommand? drillCommand = null,
        RelayCommand? jumpToTemplateCommand = null)
    {
        Entry = entry;
        _editValue = entry.Value ?? string.Empty;
        _commit = commit;
        CommitEditCommand = new AsyncRelayCommand(
            () => _commit?.Invoke(this) ?? Task.CompletedTask,
            () => !Entry.IsReadOnly);
        DrillCommand = drillCommand;
        JumpToTemplateCommand = jumpToTemplateCommand;
    }
```

2. Add the two new public members after `DrillCommand`:

```csharp
    public RelayCommand? JumpToTemplateCommand { get; }

    /// <summary>Visibility for the jump-to-template button in the Name column.</summary>
    public Microsoft.UI.Xaml.Visibility JumpToTemplateVisibility =>
        JumpToTemplateCommand is not null
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
```

- [x] **Step 4: Run the tests and verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~JumpToTemplateCommand"
```

Expected: 2 tests PASS.

- [x] **Step 5: Run the full Inspector test suite**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: All tests PASS.

- [x] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs
git commit -m "feat: add JumpToTemplateCommand, JumpToTemplateVisibility to PropertyRowViewModel"
```

---

## Task 6: MainViewModel — JumpToTemplateRoot + wire command + tests

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`
- Modify: `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`

- [x] **Step 1: Write the failing tests**

Append these four test methods to `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` (inside the class before the closing `}`):

```csharp
    [Fact]
    public void JumpToTemplateRoot_FindsDirectChild_SetsSelectedNode()
    {
        var vm = new MainViewModel();
        var childNode = new Snaipe.Protocol.ElementNode
            { Id = "2", TypeName = "Border", TemplateOrigin = "ControlTemplate" };
        var rootNode = new Snaipe.Protocol.ElementNode { Id = "1", TypeName = "Button" };
        var childVm = new TreeNodeViewModel(childNode);
        var rootVm = new TreeNodeViewModel(rootNode);
        rootVm.Children.Add(childVm);
        vm.RootNodes.Add(rootVm);
        vm.SelectedNode = rootVm;

        vm.JumpToTemplateRoot("ControlTemplate");

        Assert.Equal(childVm, vm.SelectedNode);
    }

    [Fact]
    public void JumpToTemplateRoot_FindsDeepDescendant_SetsSelectedNode()
    {
        var vm = new MainViewModel();
        var deepNode = new Snaipe.Protocol.ElementNode
            { Id = "3", TypeName = "Grid", TemplateOrigin = "ContentTemplate" };
        var midNode = new Snaipe.Protocol.ElementNode { Id = "2", TypeName = "Border" };
        var rootNode = new Snaipe.Protocol.ElementNode { Id = "1", TypeName = "ContentPresenter" };
        var deepVm = new TreeNodeViewModel(deepNode);
        var midVm = new TreeNodeViewModel(midNode);
        midVm.Children.Add(deepVm);
        var rootVm = new TreeNodeViewModel(rootNode);
        rootVm.Children.Add(midVm);
        vm.RootNodes.Add(rootVm);
        vm.SelectedNode = rootVm;

        vm.JumpToTemplateRoot("ContentTemplate");

        Assert.Equal(deepVm, vm.SelectedNode);
    }

    [Fact]
    public void JumpToTemplateRoot_NoMatch_SelectedNodeUnchanged()
    {
        var vm = new MainViewModel();
        var rootNode = new Snaipe.Protocol.ElementNode { Id = "1", TypeName = "Button" };
        var rootVm = new TreeNodeViewModel(rootNode);
        vm.RootNodes.Add(rootVm);
        vm.SelectedNode = rootVm;

        vm.JumpToTemplateRoot("ControlTemplate");

        Assert.Equal(rootVm, vm.SelectedNode);
    }

    [Fact]
    public void JumpToTemplateRoot_MultipleMatches_SelectsFirstDfsMatch()
    {
        var vm = new MainViewModel();
        var firstNode = new Snaipe.Protocol.ElementNode
            { Id = "2", TypeName = "Border", TemplateOrigin = "ItemTemplate" };
        var secondNode = new Snaipe.Protocol.ElementNode
            { Id = "3", TypeName = "Grid", TemplateOrigin = "ItemTemplate" };
        var rootNode = new Snaipe.Protocol.ElementNode { Id = "1", TypeName = "ListView" };
        var firstVm = new TreeNodeViewModel(firstNode);
        var secondVm = new TreeNodeViewModel(secondNode);
        var rootVm = new TreeNodeViewModel(rootNode);
        rootVm.Children.Add(firstVm);
        rootVm.Children.Add(secondVm);
        vm.RootNodes.Add(rootVm);
        vm.SelectedNode = rootVm;

        vm.JumpToTemplateRoot("ItemTemplate");

        Assert.Equal(firstVm, vm.SelectedNode);
    }
```

- [x] **Step 2: Run the failing tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~JumpToTemplateRoot"
```

Expected: 4 tests FAIL — `JumpToTemplateRoot` does not exist on `MainViewModel`.

- [x] **Step 3: Add ScrollIntoViewRequested event and JumpToTemplateRoot to MainViewModel**

In `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`, add these members.

After the `NavigateToBreadcrumbCommand` declaration in the Commands section (around line 118), add:

```csharp
    // ── Template jump ─────────────────────────────────────────────────────────
    /// <summary>
    /// Raised when the tree should scroll to bring a node into view.
    /// ElementTreeControl.xaml.cs subscribes and calls TreeView.ScrollIntoView.
    /// </summary>
    public event Action<TreeNodeViewModel>? ScrollIntoViewRequested;

    /// <summary>
    /// DFS-searches the children of the currently selected node for the first
    /// descendant whose <see cref="ElementNode.TemplateOrigin"/> matches
    /// <paramref name="templateOriginKind"/>, then selects it.
    /// </summary>
    public void JumpToTemplateRoot(string templateOriginKind)
    {
        if (_selectedNode is null) return;
        var found = FindTemplateRoot(_selectedNode.Children, templateOriginKind);
        if (found is null) return;
        SelectedNode = found;
        ScrollIntoViewRequested?.Invoke(found);
    }

    private static TreeNodeViewModel? FindTemplateRoot(
        IEnumerable<TreeNodeViewModel> nodes, string kind)
    {
        foreach (var node in nodes)
        {
            if (node.Node.TemplateOrigin == kind) return node;
            var found = FindTemplateRoot(node.Children, kind);
            if (found is not null) return found;
        }
        return null;
    }
```

- [x] **Step 4: Wire JumpToTemplateCommand in LoadPropertiesAsync**

In `LoadPropertiesAsync`, inside the `rows` Select lambda, find the block that creates `drillCmd`:

```csharp
                PropertyRowViewModel? row = null;
                RelayCommand? drillCmd = prop.IsObjectValued
                    ? new RelayCommand(() => DrillIntoCommand.Execute(row))
                    : null;
                row = new PropertyRowViewModel(prop,
                    r => SetPropertyAsync(node.Node.Id, capturedPath, r.Entry.Name, r.EditValue, r),
                    drillCmd);
```

Replace it with:

```csharp
                PropertyRowViewModel? row = null;
                RelayCommand? drillCmd = prop.IsObjectValued
                    ? new RelayCommand(() => DrillIntoCommand.Execute(row))
                    : null;
                RelayCommand? jumpCmd = prop.TemplateOriginKind is { } kind
                    ? new RelayCommand(() => JumpToTemplateRoot(kind))
                    : null;
                row = new PropertyRowViewModel(prop,
                    r => SetPropertyAsync(node.Node.Id, capturedPath, r.Entry.Name, r.EditValue, r),
                    drillCmd,
                    jumpCmd);
```

- [x] **Step 5: Run the failing tests to verify they now pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~JumpToTemplateRoot"
```

Expected: 4 tests PASS.

- [x] **Step 6: Run the full Inspector test suite**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: All tests PASS.

- [x] **Step 7: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs tests/Snaipe.Inspector.Tests/MainViewModelTests.cs
git commit -m "feat: add JumpToTemplateRoot, ScrollIntoViewRequested to MainViewModel; wire jump command in LoadPropertiesAsync"
```

---

## Task 7: XAML — tree badge chip, jump-arrow button, scroll support

**Files:**
- Modify: `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml`
- Modify: `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs`
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`

No unit tests for XAML changes — verified by build + smoke test.

- [x] **Step 1: Add template badge chip to ElementTreeControl.xaml**

In `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml`, replace the `<TreeView.ItemTemplate>` DataTemplate contents (the single `TextBlock`) with a `StackPanel` that includes the badge:

```xml
        <TreeView x:Name="ElementTree"
                  Grid.Row="1"
                  SelectionMode="Single">
            <TreeView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal" Spacing="6">
                        <TextBlock Text="{Binding Content.DisplayName}"
                                   FontSize="12"
                                   VerticalAlignment="Center"/>
                        <Border Background="#33007AFF"
                                CornerRadius="4"
                                Padding="4,1"
                                VerticalAlignment="Center"
                                Visibility="{Binding Content.TemplateLabelVisibility}">
                            <TextBlock Text="{Binding Content.TemplateLabel}"
                                       FontSize="10"
                                       Foreground="#007AFF"/>
                        </Border>
                    </StackPanel>
                </DataTemplate>
            </TreeView.ItemTemplate>
        </TreeView>
```

- [x] **Step 2: Add _nodeMap and scroll support to ElementTreeControl.xaml.cs**

Replace the full contents of `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs` with:

```csharp
// src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ElementTreeControl : UserControl
{
    private MainViewModel? _subscribedVm;
    private readonly Dictionary<TreeNodeViewModel, TreeViewNode> _nodeMap = new();

    public ElementTreeControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ElementTree.ItemInvoked += OnItemInvoked;
        ElementTree.Expanding += OnNodeExpanding;
        ElementTree.Collapsed += OnNodeCollapsed;
    }

    public MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        Bindings.Update();

        if (_subscribedVm is not null)
        {
            _subscribedVm.RootNodes.CollectionChanged -= OnRootNodesChanged;
            _subscribedVm.ScrollIntoViewRequested -= OnScrollIntoViewRequested;
        }

        _subscribedVm = args.NewValue as MainViewModel;

        if (_subscribedVm is not null)
        {
            _subscribedVm.RootNodes.CollectionChanged += OnRootNodesChanged;
            _subscribedVm.ScrollIntoViewRequested += OnScrollIntoViewRequested;
            RebuildTree(_subscribedVm.RootNodes);
        }
    }

    private void OnRootNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            RebuildTree(vm.RootNodes);
    }

    private void RebuildTree(System.Collections.ObjectModel.ObservableCollection<TreeNodeViewModel> roots)
    {
        _nodeMap.Clear();
        ElementTree.RootNodes.Clear();
        foreach (var root in roots)
            ElementTree.RootNodes.Add(BuildNode(root));
    }

    private TreeViewNode BuildNode(TreeNodeViewModel vm)
    {
        var node = new TreeViewNode
        {
            Content = vm,
            IsExpanded = vm.IsExpanded,
        };
        _nodeMap[vm] = node;
        foreach (var child in vm.Children)
            node.Children.Add(BuildNode(child));
        return node;
    }

    private void OnScrollIntoViewRequested(TreeNodeViewModel vm)
    {
        if (_nodeMap.TryGetValue(vm, out var tvNode))
            ElementTree.ScrollIntoView(tvNode);
    }

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
        {
            if (ViewModel is { } viewModel)
                viewModel.SelectedNode = vm;
        }
    }

    private void OnNodeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Item is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
            vm.IsExpanded = true;
    }

    private void OnNodeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (args.Item is TreeViewNode tvNode && tvNode.Content is TreeNodeViewModel vm)
            vm.IsExpanded = false;
    }
}
```

- [x] **Step 3: Add jump-arrow button to PropertyGridControl.xaml**

In `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`, find the Name column `<Grid Grid.Column="0">` block (around line 193). Replace it with a version that stacks the jump and drill buttons:

```xml
                        <!-- Name column: label + jump-to-template arrow + drill chevron -->
                        <Grid Grid.Column="0">
                            <TextBlock Text="{x:Bind Entry.Name}"
                                       FontSize="12"
                                       VerticalAlignment="Center"
                                       TextTrimming="CharacterEllipsis"
                                       HorizontalAlignment="Left"
                                       ToolTipService.ToolTip="{x:Bind Entry.Name}"/>
                            <StackPanel Orientation="Horizontal"
                                        HorizontalAlignment="Right"
                                        VerticalAlignment="Center">
                                <Button Content="↗"
                                        Visibility="{x:Bind JumpToTemplateVisibility}"
                                        Command="{x:Bind JumpToTemplateCommand}"
                                        Style="{StaticResource ChevronButtonStyle}"/>
                                <Button Content="›"
                                        Visibility="{x:Bind DrillVisibility}"
                                        Command="{x:Bind DrillCommand}"
                                        Style="{StaticResource ChevronButtonStyle}"/>
                            </StackPanel>
                        </Grid>
```

- [x] **Step 4: Build the Inspector project**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet
```

Expected: Build succeeded, 0 error(s).

- [x] **Step 5: Run all tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: All tests PASS.

- [x] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/Controls/ElementTreeControl.xaml src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs src/Snaipe.Inspector/Controls/PropertyGridControl.xaml
git commit -m "feat: add template badge chip to tree, jump-arrow button to property grid, scroll-into-view support"
```

---

## Smoke Test Checklist

After all tasks are complete, run the Inspector + SampleApp and verify manually:

- [ ] Select a `Button` → property grid shows a `ControlTemplate` row with `↗` jump arrow
- [ ] Click `↗` on `ControlTemplate` → tree scrolls to and selects the template root node; property grid loads that node's properties
- [ ] A node that is a ControlTemplate root shows a `[ControlTemplate]` badge chip in the tree
- [ ] Select a `ListView` with an `ItemTemplate` → a child node shows badge `[ItemTemplate ×N]`
- [ ] Click `↗` on `ItemTemplate` → tree selects the first realized item's template root
- [ ] Select an element with no templates → no `↗` arrows appear in the property grid
