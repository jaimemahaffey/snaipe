# Property Grid Redesign — Design Spec

**Date:** 2026-03-30
**Branch:** feature/property-editor-feedback

---

## Goal

Replace the current nested `ItemsControl` property grid with a searchable, sortable, column-based DataGrid-style layout. All rows are flat (no category group headers); the Category column provides visual grouping when sorted by category (the default).

---

## Columns

| Column | Width | Binding | Notes |
|---|---|---|---|
| Name | 2fr | `Entry.Name` | Truncated with tooltip |
| Value | 2fr | Editor via `PropertyEditorTemplateSelector` | Editable, type-specific |
| Type | 1fr | `Entry.ValueType` | Read-only display |
| Category | 1.5fr | `Entry.Category` | Read-only display |
| R/O | 50px | `Entry.IsReadOnly` | ✓ if read-only, — otherwise |

---

## New Component: `PropertyGridViewModel`

A new `ViewModels/PropertyGridViewModel.cs` encapsulates all property grid state, extracted from `MainViewModel` to keep concerns separated.

### State

| Member | Type | Default | Purpose |
|---|---|---|---|
| `_allProperties` | `List<PropertyRowViewModel>` | `[]` | Flat backing store |
| `FilteredProperties` | `ObservableCollection<PropertyRowViewModel>` | `[]` | ListView ItemsSource |
| `SearchText` | `string` | `""` | Real-time name filter |
| `ActiveSortColumn` | `string` | `"Category"` | Currently sorted column |
| `SortAscending` | `bool` | `true` | Sort direction |

### Methods

**`Load(IEnumerable<PropertyRowViewModel> rows)`**
Replaces `_allProperties`, then calls `RebuildFilteredProperties()`.

**`Clear()`**
Clears `_allProperties` and `FilteredProperties`.

**`RebuildFilteredProperties()`**
1. Filter: `_allProperties` where `Entry.Name` contains `SearchText` (case-insensitive). Empty search = all rows.
2. Sort by `ActiveSortColumn` + `SortAscending`:
   - `"Category"` → `OrderBy(Category).ThenBy(Name)`
   - `"Name"` → `OrderBy/ByDescending(Name)`
   - `"Value"` → `OrderBy/ByDescending(Entry.Value)`
   - `"Type"` → `OrderBy/ByDescending(Entry.ValueType)`
   - `"ReadOnly"` → `OrderBy/ByDescending(Entry.IsReadOnly)`
3. Rebuild `FilteredProperties` from sorted result.

### Commands

**`SortByCommand: RelayCommand<string>`** — takes a column name string.
- If `ActiveSortColumn == column`: toggle `SortAscending`.
- Else: set `ActiveSortColumn = column`, `SortAscending = true`.
- Then call `RebuildFilteredProperties()` and raise all sort indicator properties.

Requires adding a `RelayCommand<T>` generic helper to `ViewModels/RelayCommand.cs` (or a new `RelayCommandT.cs`). Standard pattern: `Execute(T parameter)` and `CanExecute(T parameter)` delegates, implementing `ICommand` with `object` cast.

### Sort indicator properties

`x:Bind` cannot invoke methods with string literal arguments. Instead, expose five computed string properties — one per column — each returning `" ↑"`, `" ↓"`, or `""`:

- `NameSortIndicator`
- `ValueSortIndicator`
- `TypeSortIndicator`
- `CategorySortIndicator`
- `ReadOnlySortIndicator`

All five are raised via `OnPropertyChanged` whenever `ActiveSortColumn` or `SortAscending` changes. Each delegates to a private `SortIndicator(string column)` helper.

---

## Changes to `MainViewModel`

- Add `public PropertyGridViewModel PropertyGrid { get; } = new()`
- `OnSelectedNodeChangedAsync`:
  - Add `CancellationTokenSource _propertiesCts` field. Cancel and replace at method entry (guards against in-flight fetches when the user rapidly selects nodes).
  - Call `PropertyGrid.Clear()` immediately on entry (before the async fetch).
  - Build flat `List<PropertyRowViewModel>` (each row wired to `SetPropertyAsync` as before).
  - Call `PropertyGrid.Load(rows)` instead of building `PropertyGroups`.
- `ClearSession()`: call `PropertyGrid.Clear()` instead of `PropertyGroups.Clear()`.
- **Remove**: `PropertyGroups` property and all `PropertyGroupViewModel` building logic.

---

## Changes to `PropertyGridControl.xaml`

Replaced entirely. New structure (top to bottom):

```
Grid (root)
├── Row 0: TextBox (search bar) — bound to ViewModel.SearchText TwoWay
├── Row 1: Grid (sticky column headers, 5 columns)
│           Each column: Button → SortByCommand with column name
│           Button content: "NAME{SortIndicator("Name")}" etc.
└── Row 2: ListView — ItemsSource={x:Bind ViewModel.FilteredProperties}
               ItemTemplate: Grid with 5 columns matching header widths
               Value cell: ContentPresenter + PropertyEditorTemplateSelector (unchanged)
```

Column proportions: `2* 2* 1* 1.5* 50`

Read-only rows: slight opacity reduction (`Opacity="0.65"`) on the row to visually distinguish them.

---

## Changes to `PropertyGridControl.xaml.cs`

- `ViewModel` property: `DataContext as PropertyGridViewModel` (was `MainViewModel`)
- No other logic needed — sort commands are bound directly in XAML via `x:Bind ViewModel.SortByCommand`

---

## Changes to `MainWindow.xaml`

```xml
<controls:PropertyGridControl DataContext="{x:Bind ViewModel.PropertyGrid}"/>
```

Passes `PropertyGridViewModel` directly as the control's DataContext instead of inheriting `MainViewModel`.

---

## `PropertyGroupViewModel` Fate

Once `PropertyGroups` is removed from `MainViewModel`, `PropertyGroupViewModel` is unused and can be deleted.

---

## In-Flight Selection Guard

`MainViewModel` gains a `CancellationTokenSource? _propertiesCts` field. At the top of `OnSelectedNodeChangedAsync`:

```csharp
_propertiesCts?.Cancel();
_propertiesCts = new CancellationTokenSource();
var ct = _propertiesCts.Token;
PropertyGrid.Clear();
```

Pass `ct` to the async fetch. On `OperationCanceledException`, return silently (do not update status or show error).

---

## Testing

New file: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`

| Test | Verifies |
|---|---|
| `Load_PopulatesFilteredProperties` | All rows appear after `Load()` |
| `SearchText_FiltersOnName_CaseInsensitive` | Partial, case-insensitive match works |
| `SearchText_Empty_ShowsAllRows` | Clearing search restores full list |
| `SortByName_SortsAscending` | Rows sorted by name A→Z |
| `SortByName_Twice_SortsDescending` | Second call on same column reverses direction |
| `SortByCategory_SortsByCategoryThenName` | Default sort produces correct order |
| `Clear_EmptiesFilteredProperties` | `Clear()` leaves grid empty |

Existing `MainViewModelTests`: update any assertions referencing `PropertyGroups` to use `PropertyGrid.FilteredProperties`.

---

## Files Touched

| File | Action |
|---|---|
| `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs` | **New** |
| `src/Snaipe.Inspector/ViewModels/RelayCommand.cs` | **Modified** (add `RelayCommand<T>`) |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | **Modified** |
| `src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs` | **Deleted** |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | **Replaced** |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs` | **Modified** |
| `src/Snaipe.Inspector/MainWindow.xaml` | **Modified** (DataContext on PropertyGridControl) |
| `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs` | **New** |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | **Modified** (update PropertyGroups refs) |
