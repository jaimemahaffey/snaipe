# DataContext / ViewModel Drill-Down — Design Spec

**Date:** 2026-03-31
**Status:** Approved
**Branch target:** `main`

---

## Goal

Allow the user to click any object-valued property row in the property grid and "drill into" it, replacing the grid with the nested object's own properties. A breadcrumb trail lets the user navigate back to any prior level. All drill depths support editing (not read-only).

---

## Behavior

- Any `PropertyEntry` with `IsObjectValued = true` shows a `>` chevron affordance in the Name column.
- Clicking it replaces the property grid contents with the properties of that nested object.
- A breadcrumb row above the search box shows the navigation path, e.g. `Button > DataContext > Address`. Clicking any crumb navigates back to that depth.
- Selecting a different element in the element tree always resets the drill-down stack to the root level.
- Editing a property at any drill depth works identically to editing a top-level property.
- If a path segment resolves to `null` at fetch time, the agent returns `ElementNotFound` and the inspector pops back to root.

---

## Architecture

### Approach

**Path-based addressing** — `GetPropertiesRequest` and `SetPropertyRequest` gain an optional `PropertyPath: string[]`. The agent traverses from the element root by following CLR property names. No agent-side object registry; the agent remains stateless.

---

## Section 1: Protocol (`Snaipe.Protocol`)

**`GetPropertiesRequest`** — add optional path:

```csharp
public sealed record GetPropertiesRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    /// <summary>
    /// Path of CLR property names to traverse from the element root before reading properties.
    /// Empty or null = read properties of the element itself (existing behaviour).
    /// </summary>
    public string[]? PropertyPath { get; init; }
}
```

**`SetPropertyRequest`** — add optional path:

```csharp
public sealed record SetPropertyRequest : InspectorMessage
{
    public required string ElementId { get; init; }
    public string[]? PropertyPath { get; init; }   // path to parent object; PropertyName is the leaf
    public required string PropertyName { get; init; }
    public required string NewValue { get; init; }
}
```

**`PropertyEntry`** — add drillability flag:

```csharp
public sealed class PropertyEntry
{
    // ... existing fields unchanged ...

    /// <summary>
    /// True when ValueKind is "Object" and the value is non-null.
    /// Inspector uses this to render the drill-down chevron affordance.
    /// </summary>
    public bool IsObjectValued { get; init; }
}
```

All existing callers continue to work unchanged — `PropertyPath` defaults to `null` and `IsObjectValued` defaults to `false`.

---

## Section 2: Agent (`Snaipe.Agent`)

### `PropertyPathResolver` (new static class)

Traverses a path of CLR property names starting from a `DependencyObject`:

```
element → GetValue(DataContextProperty) for "DataContext"
        → prop.GetValue(obj) for all other segments
```

- Returns `(object resolved, Type type)` for the object at the end of the path.
- Throws `SnaipeProtocolException(ErrorCodes.ElementNotFound)` if any segment is null or the property name is not found.
- The first segment named `"DataContext"` is resolved via `FrameworkElement.DataContextProperty`; all other segments use `Type.GetProperty(name, Public | Instance)?.GetValue(obj)`.

### `ObjectPropertyReader` (new static class)

Reads all public instance CLR properties of an arbitrary object:

```csharp
public static List<PropertyEntry> GetProperties(object obj)
```

- Enumerates `obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)`.
- For each property: `Category = "Properties"`, `ValueKind` via existing `GetValueKind`, `IsObjectValued = ValueKind == "Object" && value != null`, `IsReadOnly = !prop.CanWrite`.
- `Value` formatted using the existing `FormatValue` helper (moved to a shared location or duplicated).
- Properties whose getter throws are silently skipped.

### `ObjectPropertyWriter` (new static class)

Sets a single public instance CLR property on an arbitrary object:

```csharp
public static SetPropertyResult SetProperty(object obj, string propertyName, string newValue)
```

- Finds `obj.GetType().GetProperty(propertyName, Public | Instance)`.
- Returns `PropertyNotFound` error if missing, `PropertyReadOnly` error if `!CanWrite`.
- Parses `newValue` using the same `ParseValue` logic as `PropertyWriter`.
- Calls `prop.SetValue(obj, parsedValue)` and returns `SetPropertyResult(true, NormalizedValue: ...)`.

### `AgentIpcServer` changes

**`HandleGetProperties`:**
- If `request.PropertyPath` is non-empty: call `PropertyPathResolver.Resolve(element, path)`, then `ObjectPropertyReader.GetProperties(resolved)`.
- Otherwise: existing `PropertyReader.GetProperties(element)` path unchanged.

**`HandleSetProperty`:**
- If `request.PropertyPath` is non-empty: resolve to the *parent* object (all path segments), then call `ObjectPropertyWriter.SetProperty(parent, request.PropertyName, request.NewValue)`.
- Otherwise: existing `PropertyWriter.SetProperty(element, ...)` path unchanged.

---

## Section 3: Inspector (`Snaipe.Inspector`)

### `BreadcrumbSegment` record (new, in `ViewModels/`)

```csharp
public record BreadcrumbSegment(string Label, string[] Path);
```

`Path` is the full path needed to reach this level. The root segment always has `Path = []`.

### `MainViewModel` changes

**New state:**
```csharp
public ObservableCollection<BreadcrumbSegment> Breadcrumb { get; } = [];
```

**New commands:**
```csharp
public RelayCommand<PropertyRowViewModel> DrillIntoCommand { get; }
public RelayCommand<BreadcrumbSegment>   NavigateToBreadcrumbCommand { get; }
```

**Behavior:**
- `OnSelectedNodeChangedAsync` resets `Breadcrumb` to `[ BreadcrumbSegment(node.TypeName, []) ]` before fetching. Path passed to `GetPropertiesRequest` is `[]`.
- `DrillIntoCommand(row)`: appends `BreadcrumbSegment(row.Entry.Name, currentPath + row.Entry.Name)` and re-fetches with the new path.
- `NavigateToBreadcrumbCommand(crumb)`: truncates `Breadcrumb` to that crumb (inclusive) and re-fetches with `crumb.Path`.
- `SetPropertyAsync` builds `SetPropertyRequest` with `PropertyPath = Breadcrumb.Last().Path` (empty at root = today's behaviour).

### `PropertyGridControl` changes

**New dependency property on `PropertyGridControl`:**
```csharp
public MainViewModel? Host { get; set; }  // set via x:Bind ViewModel in MainWindow
```

**XAML additions:**
- New `Row="0"` breadcrumb row: horizontal `ItemsControl` bound to `Host.Breadcrumb`. Each segment is a `Button` that fires `Host.NavigateToBreadcrumbCommand` with the segment, with `>` `TextBlock` separators. Hidden (collapsed) when `Breadcrumb.Count <= 1`.
- Existing search box moves to `Row="1"`, column headers to `Row="2"`, `ListView` to `Row="3"`.
- Each row in the `ListView` gets a chevron `Button` in the Name column, visible only when `IsObjectValued = true`, that fires `Host.DrillIntoCommand`.

### `MainWindow.xaml` changes

Pass `ViewModel` as `Host` on the control:
```xml
<controls:PropertyGridControl Grid.Row="0"
    DataContext="{x:Bind ViewModel.PropertyGrid}"
    Host="{x:Bind ViewModel}"/>
```

---

## Section 4: Testing

### New test classes

**`ObjectPropertyReaderTests`**
- `GetProperties_ReturnsAllPublicReadableProperties`
- `GetProperties_ObjectValuedProperty_SetsIsObjectValued`
- `GetProperties_NullObjectValuedProperty_IsObjectValuedFalse`
- `GetProperties_SkipsThrowingProperties`
- `GetProperties_ReadOnlyProperty_IsReadOnlyTrue`

**`ObjectPropertyWriterTests`**
- `SetProperty_WritesStringValue`
- `SetProperty_WritesNumericValue`
- `SetProperty_ReadOnlyProperty_ReturnsPropertyReadOnlyError`
- `SetProperty_UnknownProperty_ReturnsPropertyNotFoundError`

**`PropertyPathResolverTests`**
- `Resolve_EmptyPath_ReturnsOriginalObject`
- `Resolve_SingleSegment_ReturnsPropertyValue`
- `Resolve_MultiSegment_TraversesChain`
- `Resolve_NullSegment_ThrowsElementNotFound`
- `Resolve_MissingProperty_ThrowsElementNotFound`

**`MainViewModelTests` additions**
- `DrillInto_PushesBreadcrumbSegment`
- `NavigateToBreadcrumb_PopsToCorrectDepth`
- `SelectNewNode_ResetsBreadcrumb`

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Protocol/ElementNode.cs` | Modify — add `IsObjectValued` to `PropertyEntry` |
| `src/Snaipe.Protocol/Messages.cs` | Modify — add `PropertyPath` to `GetPropertiesRequest` and `SetPropertyRequest` |
| `src/Snaipe.Agent/PropertyPathResolver.cs` | New |
| `src/Snaipe.Agent/ObjectPropertyReader.cs` | New |
| `src/Snaipe.Agent/ObjectPropertyWriter.cs` | New |
| `src/Snaipe.Agent/AgentIpcServer.cs` | Modify — route path-bearing requests to new classes |
| `src/Snaipe.Inspector/ViewModels/BreadcrumbSegment.cs` | New |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Modify — add `Breadcrumb`, `DrillIntoCommand`, `NavigateToBreadcrumbCommand`, forward `PropertyPath` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | Modify — add breadcrumb row, chevron affordance |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs` | Modify — add `Host` dependency property |
| `src/Snaipe.Inspector/MainWindow.xaml` | Modify — pass `Host="{x:Bind ViewModel}"` |
| `tests/Snaipe.Inspector.Tests/ObjectPropertyReaderTests.cs` | New |
| `tests/Snaipe.Inspector.Tests/ObjectPropertyWriterTests.cs` | New |
| `tests/Snaipe.Inspector.Tests/PropertyPathResolverTests.cs` | New |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | Modify — add breadcrumb/drill tests |
