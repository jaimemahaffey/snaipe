# DataTemplate Visualization — Design Spec

**Date:** 2026-03-31
**Status:** Approved for implementation

---

## Goal

When the Snaipe Inspector encounters a node that is the root of an instantiated DataTemplate, ControlTemplate, or ContentTemplate, surface that fact in the visual tree and provide one-click navigation from the hosting element's property grid to the template root node.

---

## Scope

Three template kinds are covered:

| Kind | Host element | Detection signal |
|---|---|---|
| `ControlTemplate` | `Control` with `Template != null` | First visual child of the Control |
| `ContentTemplate` | `ContentPresenter` with `ContentTemplate != null`, not inside an item container | First visual child of the ContentPresenter |
| `ItemTemplate` | `ContentPresenter` with `ContentTemplate != null`, inside an item container (`ListViewItem`, `GridViewItem`, etc.) | First visual child of the ContentPresenter; count = realized containers in visual tree |

---

## Architecture

Approach A — tree annotation at walk time + client-side tree search for navigation. No new protocol request/response messages.

```
VisualTreeWalker          ElementNode.TemplateOrigin / TemplateInstanceCount
      │                              │
      ▼                              ▼
PropertyReader            PropertyEntry.TemplateOriginKind
      │                              │
      ▼                              ▼
SnaipeAgent          ──────► Inspector (RootNodes tree already held)
                                     │
                          JumpToTemplateRoot(kind)
                                     │
                          DFS search RootNodes for
                          first descendant with matching
                          TemplateOrigin → set SelectedNode
```

---

## Section 1: Protocol

### `ElementNode` (modify)

Add to `src/Snaipe.Protocol/ElementNode.cs`:

```csharp
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
```

### `PropertyEntry` (modify)

Add to the `PropertyEntry` record in `src/Snaipe.Protocol/ElementNode.cs`:

```csharp
/// <summary>
/// When set, this property row is a template navigation target.
/// The Inspector renders a jump arrow and on click performs a client-side
/// DFS search for a descendant node with matching TemplateOrigin.
/// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
/// </summary>
public string? TemplateOriginKind { get; init; }
```

No new request or response message types are needed.

---

## Section 2: Agent

### `VisualTreeWalker` (modify)

`src/Snaipe.Agent/VisualTreeWalker.cs`

**Change:** Thread the parent `DependencyObject` through the `Walk` recursive call. Before constructing each `ElementNode`, call `DetectTemplateOrigin(child, parent)` to determine whether the node is a template root.

```
DetectTemplateOrigin(child, parent):

  if parent is Control with Template != null
      and child == VisualTreeHelper.GetChild(parent, 0)
    → TemplateOrigin = "ControlTemplate"

  if parent is ContentPresenter with ContentTemplate != null
      and child == VisualTreeHelper.GetChild(parent, 0)
    → walk up visual parent chain (max ~5 levels) looking for
      a ListViewItem / GridViewItem / SelectorItem ancestor
      - found  → TemplateOrigin = "ItemTemplate"
                  TemplateInstanceCount = count of realized
                  item containers in the ItemsControl's visual subtree
      - not found → TemplateOrigin = "ContentTemplate"
```

Detection adds parent-chain walking only at `ContentPresenter` nodes, not at every node. The max-5-level cap prevents unbounded upward traversal.

### `PropertyReader` (modify)

`src/Snaipe.Agent/PropertyReader.cs` — `GetTemplateEntries`

Each existing template property row gains `TemplateOriginKind`:

```csharp
// ControlTemplate row
new PropertyEntry { ..., TemplateOriginKind = "ControlTemplate" }

// ContentTemplate row (on ContentPresenter)
new PropertyEntry { ..., TemplateOriginKind = "ContentTemplate" }

// ItemTemplate row (on ItemsControl)
new PropertyEntry { ..., TemplateOriginKind = "ItemTemplate" }
```

All other properties of these rows (read-only, ValueKind = "String", etc.) remain unchanged. `TemplateOriginKind` is purely a navigation signal.

---

## Section 3: Inspector

### `TreeNodeViewModel` (modify)

`src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs`

Add two computed properties:

```csharp
public string? TemplateLabel =>
    Node.TemplateOrigin switch
    {
        "ItemTemplate" when Node.TemplateInstanceCount is { } n
            => $"ItemTemplate ×{n}",
        { } origin => origin,
        _ => null
    };

public Visibility TemplateLabelVisibility =>
    TemplateLabel is not null ? Visibility.Visible : Visibility.Collapsed;
```

**Tree XAML** (`ElementTreeControl.xaml`): add a small badge chip next to the type name label, bound to `TemplateLabel` and `TemplateLabelVisibility`. Additive only — no structural change to the tree layout.

### `PropertyRowViewModel` (modify)

`src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`

Add `RelayCommand? JumpToTemplateCommand { get; }` alongside the existing `DrillCommand`. The constructor gains an optional `jumpToTemplateCommand` parameter (same pattern as `drillCommand`).

### `MainViewModel` (modify)

`src/Snaipe.Inspector/ViewModels/MainViewModel.cs`

**`LoadPropertiesAsync`:** for rows where `prop.TemplateOriginKind` is set, construct a `RelayCommand` that calls `JumpToTemplateRoot(prop.TemplateOriginKind)` and pass it as `jumpToTemplateCommand`.

**New private method `JumpToTemplateRoot(string templateOriginKind)`:**

```
1. If _selectedNode is null → return
2. DFS-walk _selectedNode.Children (and their children)
   searching for the first TreeNodeViewModel where
   Node.TemplateOrigin == templateOriginKind
3. If found:
   a. Set SelectedNode = found node
      (triggers OnSelectedNodeChangedAsync → loads properties,
       resets breadcrumb to new element)
   b. Raise ScrollIntoViewRequested event with the found node
4. If not found → no-op (template may not be realized yet)
```

**`ScrollIntoViewRequested` event:** a new `event Action<TreeNodeViewModel>?` on `MainViewModel`. `ElementTreeControl.xaml.cs` subscribes and calls `ListView.ScrollIntoView(item)` when raised. This is the same pattern used for highlight.

### `PropertyGridControl.xaml` (modify)

The property row template gains a jump-arrow button alongside the existing drill chevron, bound to `JumpToTemplateCommand`. Visible only when `JumpToTemplateCommand` is non-null. Uses a distinct icon from the drill chevron (e.g. `↗` or a tree icon) so the two affordances are visually distinct.

---

## Section 4: Testing

### Unit tests

**`TreeNodeViewModelTests`** (new or extend existing):
- `TemplateLabel_ControlTemplate_ReturnsOriginString`
- `TemplateLabel_ItemTemplateWithCount_ReturnsCountSuffix`
- `TemplateLabel_Null_ReturnsNull`
- `TemplateLabelVisibility_NullOrigin_IsCollapsed`
- `TemplateLabelVisibility_WithOrigin_IsVisible`

**`MainViewModelTests`** (extend existing):
- `JumpToTemplateRoot_FindsDirectChild_SetsSelectedNode`
- `JumpToTemplateRoot_FindsDeepDescendant_SetsSelectedNode`
- `JumpToTemplateRoot_NoMatch_SelectedNodeUnchanged`
- `JumpToTemplateRoot_MultipleMatches_SelectsFirstDfsMatch`

**`PropertyRowViewModelTests`** (extend existing):
- `JumpToTemplateCommand_WhenTemplateOriginKindSet_IsNonNull`
- `JumpToTemplateCommand_WhenTemplateOriginKindNull_IsNull`

### Smoke test checklist

- [ ] Select a `Button` → property grid shows `ControlTemplate` row with `↗` jump arrow
- [ ] Click jump on `ControlTemplate` → tree scrolls to and selects the template root; property grid loads that node's properties
- [ ] Select a `ContentControl` with `ContentTemplate` bound → click jump → lands on the stamped content root
- [ ] Select a `ListView` with `ItemTemplate` → a tree node shows badge `[ItemTemplate ×N]`
- [ ] Click jump on `ItemTemplate` → tree selects the first realized item's template root
- [ ] Select an element with no template → no jump arrows appear

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Protocol/ElementNode.cs` | Modify — add `TemplateOrigin`, `TemplateInstanceCount` to `ElementNode`; add `TemplateOriginKind` to `PropertyEntry` |
| `src/Snaipe.Agent/VisualTreeWalker.cs` | Modify — thread parent param, add `DetectTemplateOrigin` |
| `src/Snaipe.Agent/PropertyReader.cs` | Modify — set `TemplateOriginKind` on template property rows |
| `src/Snaipe.Inspector/ViewModels/TreeNodeViewModel.cs` | Modify — add `TemplateLabel`, `TemplateLabelVisibility` |
| `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs` | Modify — add `JumpToTemplateCommand` |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Modify — wire `JumpToTemplateCommand` in `LoadPropertiesAsync`, add `JumpToTemplateRoot`, add `ScrollIntoViewRequested` event |
| `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml` | Modify — add template badge chip to tree node template |
| `src/Snaipe.Inspector/Controls/ElementTreeControl.xaml.cs` | Modify — subscribe to `ScrollIntoViewRequested`, call `ListView.ScrollIntoView` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | Modify — add jump-arrow button to property row template |
| `tests/Snaipe.Inspector.Tests/TreeNodeViewModelTests.cs` | New |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | Modify — add `JumpToTemplateRoot` tests |
| `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` | Modify — add `JumpToTemplateCommand` tests |
