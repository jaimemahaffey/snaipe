# Dependency Property Value Chain — Design Spec

**Date:** 2026-03-31
**Status:** Approved

## Problem

When inspecting a dependency property in Snaipe, it is hard to understand why a property has a specific value. Multiple sources compete for each DP — a local binding, an explicit style setter, a default/theme style setter, an active visual state setter, or inherited/default values. The current Inspector shows only the winning effective value with no indication of where it came from or what other sources were trying to set it.

## Goal

For any dependency property row in the property grid, let the user reveal a **value chain panel** showing every currently-active contributing source in precedence order, with the winner highlighted and overridden entries shown dimmed with strikethrough.

## Scope

- Applies only to real dependency property rows (the ones discovered via reflection in the main DP loop).
- Does **not** apply to synthetic rows: Data Context section, Style meta rows, Visual State group rows, Template rows. Those rows get no chain button.
- Shows only **currently-active** contributors — no hypothetical values from inactive visual states.
- v1 does not detect animations (requires internal WinUI/Uno APIs not yet used).

## Architecture

Approach A — enrich `PropertyEntry` inline. The agent computes the value chain during the existing `GetProperties` call (reusing data already gathered: `ReadLocalValue`, style walk, visual state walk). The chain is embedded directly in `PropertyEntry.ValueChain`. The Inspector renders the panel instantly from cached data with no extra roundtrip.

---

## Protocol

### New type: `ValueChainEntry`

```csharp
public sealed class ValueChainEntry
{
    /// Source label. One of:
    ///   "Local" | "Binding" | "VisualState (StateName)" |
    ///   "Style" | "BasedOn Style" | "Default Style" | "Default"
    public required string Source { get; init; }

    /// Human-readable formatted value string.
    public required string Value { get; init; }

    /// True on the one entry whose value is the effective value (highest precedence present).
    public bool IsWinner { get; init; }
}
```

### Addition to `PropertyEntry`

```csharp
/// Value chain showing all currently-active contributing sources for this DP.
/// Ordered highest→lowest precedence. Null for synthetic entries and when the
/// only source is the metadata default (no interesting chain).
public List<ValueChainEntry>? ValueChain { get; init; }
```

---

## Agent (`PropertyReader`)

### `BuildValueChain` helper

New `private static List<ValueChainEntry>? BuildValueChain(DependencyObject element, DependencyProperty dp, object? effectiveValue, object? localValue)` called from the main DP loop.

**Source detection order (highest → lowest precedence):**

| # | Source label | Detection method |
|---|---|---|
| 1 | `Binding` | `localValue is BindingExpression` |
| 2 | `Local` | `localValue != DependencyProperty.UnsetValue`, not a `BindingExpression` |
| 3 | `VisualState (name)` | `TryGetActiveVisualStateSetter(fe, dp)` — walks `VisualStateManager.GetVisualStateGroups(fe)`, finds `group.CurrentState`, scans `CurrentState.Setters` for a `Setter` targeting `dp` with no `Target` (element-level setter) |
| 4 | `Style` | Walk `fe.Style.Setters` for a `Setter` whose `Property == dp` (depth 0) |
| 5 | `BasedOn Style` | Same, each level up the `BasedOn` chain (depth 1…N, max 10). All levels use the label `"BasedOn Style"` — no depth suffix needed since multiple BasedOn entries for the same property are rare in practice. |
| 6 | `Default Style` | Inferred: entries is empty after steps 1–5 AND `FormatValue(effectiveValue) != FormatValue(metadataDefault)` |
| 7 | `Default` | `dp.GetMetadata(element.GetType()).DefaultValue` — always appended as the floor |

**Winner rule:** `entries[0].IsWinner = true`. Since the list is built in precedence order, the first entry is always the winner. If the only entry is `Default`, the chain is suppressed (`return null`) — no interesting information to show.

### `TryGetActiveVisualStateSetter` helper

New `private static (bool Found, string StateName, object? Value) TryGetActiveVisualStateSetter(FrameworkElement fe, DependencyProperty dp)`.

Walks all groups in `VisualStateManager.GetVisualStateGroups(fe)`. For each group, if `group.CurrentState` is not null, scans `group.CurrentState.Setters` for a `Setter` whose `Property == dp`. Returns the first match found. Skips setters where `Target` is not null (template-internal targeting, out of v1 scope).

### Integration in `GetProperties`

In the main DP loop, after constructing the `PropertyEntry`, compute and assign the chain:

```csharp
var chain = BuildValueChain(element, dpInfo.Property, value, localValue);
entries.Add(new Protocol.PropertyEntry
{
    // ... existing fields ...
    ValueChain = chain,
});
```

`localValue` is already read for the `BindingExpression` check — no duplicate read needed.

---

## Inspector ViewModels

### New: `ValueChainEntryViewModel`

Thin display wrapper (new file):

```csharp
public sealed class ValueChainEntryViewModel
{
    public string Source { get; }
    public string Value  { get; }
    public bool   IsWinner { get; }
    public Microsoft.UI.Xaml.Visibility WinnerBadgeVisibility  // Visible when IsWinner
    public Microsoft.UI.Xaml.Visibility OverriddenVisibility   // Visible when !IsWinner
}
```

### `PropertyRowViewModel` additions

- Constructor gains one optional parameter: `RelayCommand? showValueChainCommand = null`
- `IReadOnlyList<ValueChainEntryViewModel>? ValueChain` — built from `entry.ValueChain` in constructor; null when entry has no chain
- `RelayCommand? ShowValueChainCommand`
- `Visibility ShowValueChainVisibility` — `Visible` when `ValueChain` has entries

### `PropertyGridViewModel` additions

- `ValueChainEntryViewModel[]? ActiveValueChain { get; private set; }` — null = panel hidden
- `string? ValueChainPropertyName { get; private set; }` — header label, e.g. `"Background"`
- `Visibility ValueChainPanelVisibility` — derived from `ActiveValueChain != null`
- `public void ShowValueChain(PropertyRowViewModel row)` — sets `ActiveValueChain` and `ValueChainPropertyName`; calling with the same row a second time clears both (toggle behaviour)
- `public void ClearValueChain()` — sets `ActiveValueChain = null`, `ValueChainPropertyName = null`; wired to a `ClearValueChainCommand` RelayCommand for the ✕ button
- `Clear()` updated to also call `ClearValueChain()`

### `MainViewModel.LoadPropertiesAsync` wiring

Same closure-capture pattern as `drillCmd` and `jumpCmd`:

```csharp
RelayCommand? chainCmd = prop.ValueChain is { Count: > 0 }
    ? new RelayCommand(() => PropertyGrid.ShowValueChain(row))
    : null;
row = new PropertyRowViewModel(prop,
    r => SetPropertyAsync(...),
    drillCmd,
    jumpCmd,
    chainCmd);
```

---

## Inspector XAML (`PropertyGridControl.xaml`)

### Name column button stack

The existing `<Grid Grid.Column="0">` already contains a `<StackPanel Orientation="Horizontal" HorizontalAlignment="Right">` holding `↗` and `›`. Add `?` as the leftmost button in that stack:

```xml
<Button Content="?"
        Visibility="{x:Bind ShowValueChainVisibility}"
        Command="{x:Bind ShowValueChainCommand}"
        Style="{StaticResource ChevronButtonStyle}"/>
```

### Value chain panel (new Row 4)

Add a new `<RowDefinition Height="Auto"/>` to the Grid. The row is collapsed when `ValueChainPanelVisibility == Collapsed`:

```xml
<Grid Grid.Row="4"
      Visibility="{x:Bind ViewModel.ValueChainPanelVisibility, Mode=OneWay}"
      BorderBrush="#2563EB" BorderThickness="0,2,0,0"
      Background="#0F172A"
      Padding="12,10">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <!-- Header -->
    <Grid Grid.Row="0" Margin="0,0,0,8">
        <TextBlock Text="{x:Bind ViewModel.ValueChainPropertyName, Mode=OneWay}"
                   FontSize="11" FontWeight="SemiBold" Foreground="#60A5FA"/>
        <Button Content="✕" HorizontalAlignment="Right"
                Command="{x:Bind ViewModel.ClearValueChainCommand}"
                Style="{StaticResource ChevronButtonStyle}"/>
    </Grid>

    <!-- Chain entries -->
    <ItemsControl Grid.Row="1"
                  ItemsSource="{x:Bind ViewModel.ActiveValueChain, Mode=OneWay}">
        <ItemsControl.ItemTemplate>
            <DataTemplate x:DataType="vm:ValueChainEntryViewModel">
                <!-- Winner row: green left border, highlighted background, "wins" badge -->
                <!-- Loser row: grey left border, dimmed opacity, strikethrough value -->
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Grid>
```

The DataTemplate renders winner vs. overridden rows using `WinnerBadgeVisibility` / `OverriddenVisibility` bound to distinct visual treatments (green border + `[wins]` badge for winner; grey border + strikethrough + reduced opacity for overridden).

A `ClearValueChainCommand` is added to `PropertyGridViewModel` to handle the ✕ close button (sets `ActiveValueChain = null`).

---

## Testing

### Agent tests (`Snaipe.Agent.Tests`)

`BuildValueChainTests` (new test class — requires `InternalsVisibleTo` already present):

- `BuildValueChain_LocalValue_MarkedAsWinner`
- `BuildValueChain_OnlyDefault_ReturnsNull`
- `BuildValueChain_StyleSetter_PresentAndOverriddenByLocal`
- `BuildValueChain_ActiveVisualStateSetter_DetectedCorrectly`
- `BuildValueChain_BasedOnChain_EnumeratesAllDepths`

### Inspector tests (`Snaipe.Inspector.Tests`)

`ValueChainEntryViewModelTests` (new):

- `IsWinner_True_WinnerBadgeVisible_OverriddenCollapsed`
- `IsWinner_False_WinnerBadgeCollapsed_OverriddenVisible`

`PropertyRowViewModelTests` additions:

- `ShowValueChainVisibility_WithChain_IsVisible`
- `ShowValueChainVisibility_NullChain_IsCollapsed`

`PropertyGridViewModelTests` additions:

- `ShowValueChain_SetsActiveChain`
- `ShowValueChain_SameRowTwice_Toggles`
- `Clear_ResetsActiveValueChain`
