# Property DataGrid Redesign — Design Spec

**Date:** 2026-04-04
**Topic:** datagrid-redesign

---

## Goal

Replace the current `ListView`-based property grid simulation with a true `DataGrid` from the Community Toolkit. This upgrade brings native column alignment, resizable columns, high-performance UI virtualization, and grouped categories with collapsible headers.

---

## Architecture & Dependencies

The standard control for this scenario in the WinUI/Uno ecosystem is the `CommunityToolkit.WinUI.UI.Controls.DataGrid`.

**New Packages Required:**
- `Uno.CommunityToolkit.WinUI.UI.Controls.DataGrid` (for Uno Skia/cross-platform support).
- We may also need `CommunityToolkit.WinUI.Collections` (or the equivalent Uno package) for `AdvancedCollectionView` to support data grouping and sorting.

---

## UI Layout & Columns

The grid will shift from a flat 5-column layout to a grouped 3-column layout:

1. **Name** (Width: `2*`) - Displays `Entry.Name`.
2. **Value** (Width: `2*`) - Displays the editable control using `PropertyEditorTemplateSelector`.
3. **Type** (Width: `1*`) - Displays `Entry.ValueType`.

### Category Grouping

Instead of a dedicated Category column, properties will be natively grouped by `Entry.Category`.
The DataGrid will use a `CollectionViewSource` with grouped data to render collapsible category headers above each group of properties.

### Read-Only State

The "R/O" checkbox column is removed to keep the interface clean.
Instead, read-only state (`Entry.IsReadOnly`) will be indicated visually:
- A subtle lock icon (or `FontIcon`) next to the property name.
- Dimmed text/opacity for the row.

---

## Component Updates

### `PropertyGridViewModel`
- Transition the `FilteredProperties` collection to use `AdvancedCollectionView` or a custom `ObservableCollection<IGrouping<string, PropertyRowViewModel>>`.
- Ensure sorting defaults to Category, then Name.
- Update the search/filter logic to re-apply the filter over the `AdvancedCollectionView` when `SearchText` changes.

### `PropertyGridControl.xaml`
- Replace `<ListView>` and its custom header `Grid` with `<controls:DataGrid>`.
- Configure `DataGrid.Columns`:
  - `DataGridTemplateColumn` for Name (includes lock icon if R/O, plus drill-down buttons).
  - `DataGridTemplateColumn` for Value (uses our existing `PropertyEditorTemplateSelector`).
  - `DataGridTextColumn` for Type.
- Define a `DataGrid.RowGroupHeaderStyles` or `DataGrid.RowGroupHeaderTemplate` to style the Category headers cleanly.

---

## Testing Strategy

- Update `PropertyGridViewModelTests` to verify grouping logic (e.g., verifying `AdvancedCollectionView.Groups` or the resulting grouped collection).
- Ensure filtering handles grouped items properly (empty groups should ideally be hidden).

---

## Self-Review Checklist

- **Ambiguity:** Are the column sizes explicitly defined? Yes, `2*`, `2*`, `1*`.
- **Scope:** Is this scoped effectively? Yes, it's a UI-layer replacement of a single control, preserving the underlying ViewModel logic while introducing a grouping collection view.
- **Dependencies:** The required Community Toolkit package for Uno is explicitly listed.

