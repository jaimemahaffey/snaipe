# Dependency Property Value Chain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `?` button to each dependency property row in the Inspector property grid that opens a panel showing all currently-active value sources competing for that property, with the winner highlighted and overridden sources shown with strikethrough.

**Architecture:** The agent enriches each `PropertyEntry` with a `ValueChain` list during the existing `GetProperties` call — no new roundtrip. The Inspector renders the panel instantly from cached data. A side panel in `PropertyGridControl` is toggled by a new `ShowValueChain` command on `PropertyGridViewModel`.

**Tech Stack:** C# 13, .NET 9, Uno Platform 6.5 (WinUI), xUnit

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Protocol/ElementNode.cs` | Modify — add `ValueChainEntry` class; add `ValueChain` to `PropertyEntry` |
| `src/Snaipe.Agent/PropertyReader.cs` | Modify — add `BuildValueChain`, `TryGetActiveVisualStateSetter`, `TryGetStyleSetter`; wire into DP loop |
| `src/Snaipe.Inspector/ViewModels/ValueChainEntryViewModel.cs` | Create — thin display wrapper |
| `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs` | Modify — add `ValueChain`, `ShowValueChainCommand`, `ShowValueChainVisibility` |
| `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs` | Modify — add `ActiveValueChain`, `ValueChainPropertyName`, `ValueChainPanelVisibility`, `ShowValueChain`, `ClearValueChain`, `ClearValueChainCommand` |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | Modify — wire `chainCmd` closure in `LoadPropertiesAsync` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | Modify — add `?` button to Name column; add value chain panel as Row 4 |
| `tests/Snaipe.Inspector.Tests/ValueChainEntryViewModelTests.cs` | Create — tests for `ValueChainEntryViewModel` |
| `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` | Modify — add `ShowValueChainVisibility` tests |
| `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs` | Create — tests for `ShowValueChain` / `ClearValueChain` / `Clear` |

> **Note:** Agent-side `BuildValueChain` tests are omitted from this plan. The method requires a live `DependencyObject` with WinUI runtime — not unit-testable without a UI thread. Verification is build-only for Task 2.

---

## Task 1: Protocol — ValueChainEntry + PropertyEntry.ValueChain

**Files:**
- Modify: `src/Snaipe.Protocol/ElementNode.cs`

- [ ] **Step 1: Add `ValueChainEntry` class and `ValueChain` property to `PropertyEntry`**

Replace the full contents of `src/Snaipe.Protocol/ElementNode.cs` with:

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
    /// Values: "ControlTemplate" | "ContentTemplate" | "ItemTemplate"
    /// </summary>
    public string? TemplateOriginKind { get; init; }
    /// <summary>
    /// All currently-active contributing sources for this dependency property, ordered
    /// highest→lowest precedence. Null for synthetic entries (Data Context, Style meta, etc.)
    /// and when the only source is the metadata default.
    /// </summary>
    public List<ValueChainEntry>? ValueChain { get; init; }
}

/// <summary>
/// One entry in a dependency property value chain.
/// </summary>
public sealed class ValueChainEntry
{
    /// <summary>
    /// Source label. One of:
    ///   "Local" | "Binding" | "VisualState (StateName)" |
    ///   "Style" | "BasedOn Style" | "Default Style" | "Default"
    /// </summary>
    public required string Source { get; init; }

    /// <summary>Human-readable formatted value string.</summary>
    public required string Value { get; init; }

    /// <summary>
    /// True on the one entry whose value is the effective value (highest precedence present).
    /// </summary>
    public bool IsWinner { get; init; }
}
```

- [ ] **Step 2: Build to verify no errors**

```bash
dotnet build src/Snaipe.Protocol/Snaipe.Protocol.csproj -v quiet
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 3: Commit**

```bash
git add src/Snaipe.Protocol/ElementNode.cs
git commit -m "feat: add ValueChainEntry type and PropertyEntry.ValueChain to protocol"
```

---

## Task 2: Agent — BuildValueChain and helpers

**Files:**
- Modify: `src/Snaipe.Agent/PropertyReader.cs`

- [ ] **Step 1: Add `TryGetActiveVisualStateSetter`, `TryGetStyleSetter`, and `BuildValueChain` helpers to PropertyReader**

Add these three private static methods to `PropertyReader` (before the closing `}` of the class, after the existing `FindDependencyPropertyName` method):

```csharp
    /// <summary>
    /// Checks whether the currently-active visual state on <paramref name="fe"/> has a setter
    /// targeting <paramref name="dp"/>. Returns the first match found across all groups.
    /// Skips setters with a Target property (template-internal setters, out of v1 scope).
    /// </summary>
    private static (bool Found, string StateName, object? Value) TryGetActiveVisualStateSetter(
        FrameworkElement fe, DependencyProperty dp)
    {
        try
        {
            var groups = VisualStateManager.GetVisualStateGroups(fe);
            if (groups is null) return default;

            foreach (var group in groups)
            {
                var state = group.CurrentState;
                if (state is null) continue;

                foreach (var setterBase in state.Setters)
                {
                    if (setterBase is not Setter setter) continue;
                    try
                    {
                        if (setter.Property == dp)
                            return (true, state.Name ?? "(unnamed)", setter.Value);
                    }
                    catch { }
                }
            }
        }
        catch { }
        return default;
    }

    /// <summary>
    /// Checks whether <paramref name="style"/> has a setter targeting <paramref name="dp"/>.
    /// </summary>
    private static (bool Found, object? Value) TryGetStyleSetter(Style style, DependencyProperty dp)
    {
        foreach (var setterBase in style.Setters)
        {
            if (setterBase is not Setter setter) continue;
            try
            {
                if (setter.Property == dp)
                    return (true, setter.Value);
            }
            catch { }
        }
        return default;
    }

    /// <summary>
    /// Builds the value chain for a single dependency property on <paramref name="element"/>.
    /// Returns null when the only source is the metadata default (no interesting chain).
    /// </summary>
    private static List<Protocol.ValueChainEntry>? BuildValueChain(
        DependencyObject element,
        DependencyProperty dp,
        object? effectiveValue,
        object? localValue)
    {
        var entries = new List<Protocol.ValueChainEntry>();
        var fe = element as FrameworkElement;

        // 1. Binding or Local
        if (localValue != DependencyProperty.UnsetValue)
        {
            if (localValue is BindingExpression be)
                entries.Add(new Protocol.ValueChainEntry { Source = "Binding", Value = FormatBindingExpression(be) });
            else
                entries.Add(new Protocol.ValueChainEntry { Source = "Local", Value = FormatValue(localValue) });
        }

        // 2. Active VisualState setter
        if (fe is not null)
        {
            var (found, stateName, vsValue) = TryGetActiveVisualStateSetter(fe, dp);
            if (found)
                entries.Add(new Protocol.ValueChainEntry
                    { Source = $"VisualState ({stateName})", Value = FormatValue(vsValue) });
        }

        // 3. Explicit Style + BasedOn chain
        if (fe?.Style is { } style)
        {
            var depth = 0;
            var current = style;
            while (current is not null && depth <= 10)
            {
                var (found, setterValue) = TryGetStyleSetter(current, dp);
                if (found)
                {
                    var source = depth == 0 ? "Style" : "BasedOn Style";
                    entries.Add(new Protocol.ValueChainEntry { Source = source, Value = FormatValue(setterValue) });
                }
                current = current.BasedOn;
                depth++;
            }
        }

        // 4. Default Style (inferred: value differs from metadata default but nothing above set it)
        object? metaDefault;
        try { metaDefault = dp.GetMetadata(element.GetType()).DefaultValue; }
        catch { metaDefault = null; }

        var effectiveFormatted = FormatValue(effectiveValue);
        var metaFormatted = FormatValue(metaDefault);

        if (entries.Count == 0 && effectiveFormatted != metaFormatted)
            entries.Add(new Protocol.ValueChainEntry { Source = "Default Style", Value = effectiveFormatted });

        // 5. Default (metadata floor — always appended)
        entries.Add(new Protocol.ValueChainEntry { Source = "Default", Value = metaFormatted });

        // Suppress when the only source is Default (nothing interesting)
        if (entries.Count == 1)
            return null;

        // Winner is always entries[0] (list is built in precedence order)
        entries[0] = new Protocol.ValueChainEntry
            { Source = entries[0].Source, Value = entries[0].Value, IsWinner = true };

        return entries;
    }
```

- [ ] **Step 2: Wire `BuildValueChain` into the main DP loop in `GetProperties`**

In `PropertyReader.GetProperties`, find the `entries.Add(new Protocol.PropertyEntry { ... })` inside the `foreach (var dpInfo in props)` loop. Replace it with a version that includes `ValueChain`:

```csharp
                var chain = BuildValueChain(element, dpInfo.Property, value, localValue);
                entries.Add(new Protocol.PropertyEntry
                {
                    Name = dpInfo.Name,
                    Category = CategorizeProperty(dpInfo.Name),
                    ValueType = valueType.Name,
                    Value = FormatValue(value),
                    ValueKind = kind,
                    IsReadOnly = isReadOnly,
                    IsObjectValued = kind == "Object" && value is not null,
                    BindingExpression = bindingExpression,
                    EnumValues = effectiveType.IsEnum ? Enum.GetNames(effectiveType).ToList() : null,
                    ValueChain = chain,
                });
```

- [ ] **Step 3: Build Agent project**

```bash
dotnet build src/Snaipe.Agent/Snaipe.Agent.csproj -v quiet
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Agent/PropertyReader.cs
git commit -m "feat: add BuildValueChain to PropertyReader; populate PropertyEntry.ValueChain in DP loop"
```

---

## Task 3: Inspector — ValueChainEntryViewModel + tests

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/ValueChainEntryViewModel.cs`
- Create: `tests/Snaipe.Inspector.Tests/ValueChainEntryViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Snaipe.Inspector.Tests/ValueChainEntryViewModelTests.cs`:

```csharp
using Microsoft.UI.Xaml;
using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class ValueChainEntryViewModelTests
{
    [Fact]
    public void IsWinner_True_WinnerBadgeVisible_OverriddenCollapsed()
    {
        var entry = new ValueChainEntry { Source = "Local", Value = "Blue", IsWinner = true };
        var vm = new ValueChainEntryViewModel(entry);

        Assert.Equal(Visibility.Visible,   vm.WinnerBadgeVisibility);
        Assert.Equal(Visibility.Collapsed, vm.OverriddenVisibility);
    }

    [Fact]
    public void IsWinner_False_WinnerBadgeCollapsed_OverriddenVisible()
    {
        var entry = new ValueChainEntry { Source = "Style", Value = "Red" };
        var vm = new ValueChainEntryViewModel(entry);

        Assert.Equal(Visibility.Collapsed, vm.WinnerBadgeVisibility);
        Assert.Equal(Visibility.Visible,   vm.OverriddenVisibility);
    }

    [Fact]
    public void Source_And_Value_ExposedCorrectly()
    {
        var entry = new ValueChainEntry { Source = "VisualState (PointerOver)", Value = "#FF0000", IsWinner = true };
        var vm = new ValueChainEntryViewModel(entry);

        Assert.Equal("VisualState (PointerOver)", vm.Source);
        Assert.Equal("#FF0000", vm.Value);
        Assert.True(vm.IsWinner);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ValueChainEntryViewModel" 2>&1 | tail -8
```

Expected: compile errors — `ValueChainEntryViewModel` does not exist.

- [ ] **Step 3: Create ValueChainEntryViewModel**

Create `src/Snaipe.Inspector/ViewModels/ValueChainEntryViewModel.cs`:

```csharp
using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Inspector.ViewModels;

public sealed class ValueChainEntryViewModel
{
    public ValueChainEntryViewModel(ValueChainEntry entry)
    {
        Source = entry.Source;
        Value = entry.Value;
        IsWinner = entry.IsWinner;
    }

    public string Source { get; }
    public string Value  { get; }
    public bool   IsWinner { get; }

    /// <summary>Visible on the winning entry — shows green highlight and "wins" badge.</summary>
    public Visibility WinnerBadgeVisibility =>
        IsWinner ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visible on overridden entries — shows strikethrough and dimmed opacity.</summary>
    public Visibility OverriddenVisibility =>
        IsWinner ? Visibility.Collapsed : Visibility.Visible;
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ValueChainEntryViewModel" 2>&1 | tail -6
```

Expected: Passed! — Failed: 0, Passed: 3.

- [ ] **Step 5: Run full Inspector test suite**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows 2>&1 | tail -5
```

Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/ValueChainEntryViewModel.cs tests/Snaipe.Inspector.Tests/ValueChainEntryViewModelTests.cs
git commit -m "feat: add ValueChainEntryViewModel with WinnerBadgeVisibility / OverriddenVisibility"
```

---

## Task 4: Inspector — PropertyRowViewModel additions + tests

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`
- Modify: `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these two test methods to `tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs` (inside the class, before the closing `}`):

```csharp
    [Fact]
    public void ShowValueChainVisibility_WithChain_IsVisible()
    {
        var entry = new PropertyEntry
        {
            Name = "Background", Category = "Appearance", ValueKind = "Object",
            ValueChain =
            [
                new ValueChainEntry { Source = "Local", Value = "Blue", IsWinner = true },
                new ValueChainEntry { Source = "Default", Value = "null" }
            ]
        };
        var vm = new PropertyRowViewModel(entry);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.ShowValueChainVisibility);
    }

    [Fact]
    public void ShowValueChainVisibility_NullChain_IsCollapsed()
    {
        var entry = new PropertyEntry { Name = "Width", Category = "Layout", ValueKind = "Number" };
        var vm = new PropertyRowViewModel(entry);

        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.ShowValueChainVisibility);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ShowValueChainVisibility" 2>&1 | tail -8
```

Expected: compile errors — `ShowValueChainVisibility` does not exist.

- [ ] **Step 3: Add ValueChain, ShowValueChainCommand, ShowValueChainVisibility to PropertyRowViewModel**

In `src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs`:

1. Change the constructor signature to add the optional fifth parameter:

```csharp
    public PropertyRowViewModel(PropertyEntry entry,
        Func<PropertyRowViewModel, Task>? commit = null,
        RelayCommand? drillCommand = null,
        RelayCommand? jumpToTemplateCommand = null,
        RelayCommand? showValueChainCommand = null)
    {
        Entry = entry;
        _editValue = entry.Value ?? string.Empty;
        _commit = commit;
        CommitEditCommand = new AsyncRelayCommand(
            () => _commit?.Invoke(this) ?? Task.CompletedTask,
            () => !Entry.IsReadOnly);
        DrillCommand = drillCommand;
        JumpToTemplateCommand = jumpToTemplateCommand;
        ShowValueChainCommand = showValueChainCommand;
        ValueChain = entry.ValueChain?
            .Select(e => new ValueChainEntryViewModel(e))
            .ToArray();
    }
```

2. Add these members after `JumpToTemplateVisibility`:

```csharp
    public RelayCommand? ShowValueChainCommand { get; }

    /// <summary>
    /// The value chain for this dependency property row, built from protocol data.
    /// Null for synthetic rows (Data Context, Style meta, etc.) and default-only properties.
    /// </summary>
    public IReadOnlyList<ValueChainEntryViewModel>? ValueChain { get; }

    /// <summary>Visibility for the value-chain ? button in the Name column.</summary>
    public Microsoft.UI.Xaml.Visibility ShowValueChainVisibility =>
        ValueChain is { Count: > 0 }
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~ShowValueChainVisibility" 2>&1 | tail -6
```

Expected: Passed! — Failed: 0, Passed: 2.

- [ ] **Step 5: Run full Inspector test suite**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows 2>&1 | tail -5
```

Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyRowViewModel.cs tests/Snaipe.Inspector.Tests/PropertyRowViewModelTests.cs
git commit -m "feat: add ValueChain, ShowValueChainCommand, ShowValueChainVisibility to PropertyRowViewModel"
```

---

## Task 5: Inspector — PropertyGridViewModel + MainViewModel wiring + tests

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs`
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`
- Create: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`:

```csharp
using Microsoft.UI.Xaml;
using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class PropertyGridViewModelTests
{
    private static PropertyRowViewModel MakeRowWithChain(string propertyName)
    {
        var entry = new PropertyEntry
        {
            Name = propertyName,
            Category = "Appearance",
            ValueKind = "Object",
            ValueChain =
            [
                new ValueChainEntry { Source = "Local", Value = "Blue", IsWinner = true },
                new ValueChainEntry { Source = "Default", Value = "null" }
            ]
        };
        return new PropertyRowViewModel(entry);
    }

    [Fact]
    public void ShowValueChain_SetsActiveChain()
    {
        var grid = new PropertyGridViewModel();
        var row = MakeRowWithChain("Background");

        grid.ShowValueChain(row);

        Assert.NotNull(grid.ActiveValueChain);
        Assert.Equal(Visibility.Visible, grid.ValueChainPanelVisibility);
        Assert.Contains("Background", grid.ValueChainPropertyName);
    }

    [Fact]
    public void ShowValueChain_SameRowTwice_TogglesOff()
    {
        var grid = new PropertyGridViewModel();
        var row = MakeRowWithChain("Background");

        grid.ShowValueChain(row);
        grid.ShowValueChain(row);

        Assert.Null(grid.ActiveValueChain);
        Assert.Equal(Visibility.Collapsed, grid.ValueChainPanelVisibility);
    }

    [Fact]
    public void ShowValueChain_DifferentRow_SwitchesChain()
    {
        var grid = new PropertyGridViewModel();
        var row1 = MakeRowWithChain("Background");
        var row2 = MakeRowWithChain("Foreground");

        grid.ShowValueChain(row1);
        grid.ShowValueChain(row2);

        Assert.NotNull(grid.ActiveValueChain);
        Assert.Contains("Foreground", grid.ValueChainPropertyName);
    }

    [Fact]
    public void ClearValueChain_HidesPanel()
    {
        var grid = new PropertyGridViewModel();
        grid.ShowValueChain(MakeRowWithChain("Background"));

        grid.ClearValueChain();

        Assert.Null(grid.ActiveValueChain);
        Assert.Equal(Visibility.Collapsed, grid.ValueChainPanelVisibility);
    }

    [Fact]
    public void Clear_ResetsActiveValueChain()
    {
        var grid = new PropertyGridViewModel();
        grid.ShowValueChain(MakeRowWithChain("Background"));

        grid.Clear();

        Assert.Null(grid.ActiveValueChain);
        Assert.Equal(Visibility.Collapsed, grid.ValueChainPanelVisibility);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests" 2>&1 | tail -8
```

Expected: compile errors — `ActiveValueChain`, `ShowValueChain`, etc. do not exist.

- [ ] **Step 3: Add value-chain members to PropertyGridViewModel**

In `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs`:

1. Add private backing fields after the existing fields at the top of the class:

```csharp
    private PropertyRowViewModel? _activeChainRow;
    private ValueChainEntryViewModel[]? _activeValueChain;
    private string? _valueChainPropertyName;
```

2. In the constructor, add command initialisation after `SortByCommand = new RelayCommand<string>(SortBy);`:

```csharp
        ClearValueChainCommand = new RelayCommand(ClearValueChain);
```

3. Add these public members after the `SortByCommand` property:

```csharp
    public RelayCommand ClearValueChainCommand { get; }

    public ValueChainEntryViewModel[]? ActiveValueChain
    {
        get => _activeValueChain;
        private set
        {
            if (SetField(ref _activeValueChain, value))
                OnPropertyChanged(nameof(ValueChainPanelVisibility));
        }
    }

    public string? ValueChainPropertyName
    {
        get => _valueChainPropertyName;
        private set => SetField(ref _valueChainPropertyName, value);
    }

    public Microsoft.UI.Xaml.Visibility ValueChainPanelVisibility =>
        _activeValueChain is not null
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>
    /// Shows the value chain panel for <paramref name="row"/>. Calling with the same row
    /// a second time toggles the panel off.
    /// </summary>
    public void ShowValueChain(PropertyRowViewModel row)
    {
        if (row.ValueChain is null) return;

        // Toggle: clicking same row again closes panel
        if (ReferenceEquals(_activeChainRow, row) && ActiveValueChain is not null)
        {
            ClearValueChain();
            return;
        }

        _activeChainRow = row;
        ActiveValueChain = row.ValueChain.ToArray();
        ValueChainPropertyName = $"{row.Entry.Name} — value chain";
    }

    /// <summary>Hides the value chain panel.</summary>
    public void ClearValueChain()
    {
        _activeChainRow = null;
        ActiveValueChain = null;
        ValueChainPropertyName = null;
    }
```

4. Update the existing `Clear()` method to also call `ClearValueChain()`:

```csharp
    public void Clear()
    {
        _allProperties = [];
        FilteredProperties.Clear();
        ClearValueChain();
    }
```

- [ ] **Step 4: Wire chainCmd in MainViewModel.LoadPropertiesAsync**

In `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`, find the block inside `LoadPropertiesAsync` that builds `row`:

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

Replace it with:

```csharp
                PropertyRowViewModel? row = null;
                RelayCommand? drillCmd = prop.IsObjectValued
                    ? new RelayCommand(() => DrillIntoCommand.Execute(row))
                    : null;
                RelayCommand? jumpCmd = prop.TemplateOriginKind is { } kind
                    ? new RelayCommand(() => JumpToTemplateRoot(kind))
                    : null;
                RelayCommand? chainCmd = prop.ValueChain is { Count: > 0 }
                    ? new RelayCommand(() => PropertyGrid.ShowValueChain(row!))
                    : null;
                row = new PropertyRowViewModel(prop,
                    r => SetPropertyAsync(node.Node.Id, capturedPath, r.Entry.Name, r.EditValue, r),
                    drillCmd,
                    jumpCmd,
                    chainCmd);
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests" 2>&1 | tail -6
```

Expected: Passed! — Failed: 0, Passed: 5.

- [ ] **Step 6: Run full Inspector test suite**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows 2>&1 | tail -5
```

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs src/Snaipe.Inspector/ViewModels/MainViewModel.cs tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs
git commit -m "feat: add ShowValueChain, ClearValueChain, ActiveValueChain to PropertyGridViewModel; wire chainCmd in LoadPropertiesAsync"
```

---

## Task 6: XAML — PropertyGridControl value chain panel + button

**Files:**
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`

- [ ] **Step 1: Add Row 4 definition and the `?` button to the Name column**

In `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`:

1. Find the `<Grid.RowDefinitions>` block and add a fifth row:

```xml
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
```

2. Find the Name column `<StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">` that already holds `↗` and `›`. Add `?` as the first button:

```xml
                            <StackPanel Orientation="Horizontal"
                                        HorizontalAlignment="Right"
                                        VerticalAlignment="Center">
                                <Button Content="?"
                                        Visibility="{x:Bind ShowValueChainVisibility}"
                                        Command="{x:Bind ShowValueChainCommand}"
                                        Style="{StaticResource ChevronButtonStyle}"/>
                                <Button Content="↗"
                                        Visibility="{x:Bind JumpToTemplateVisibility}"
                                        Command="{x:Bind JumpToTemplateCommand}"
                                        Style="{StaticResource ChevronButtonStyle}"/>
                                <Button Content="›"
                                        Visibility="{x:Bind DrillVisibility}"
                                        Command="{x:Bind DrillCommand}"
                                        Style="{StaticResource ChevronButtonStyle}"/>
                            </StackPanel>
```

- [ ] **Step 2: Add the value chain panel as Grid Row 4**

Add this immediately before the closing `</Grid>` at the end of `PropertyGridControl.xaml`:

```xml
        <!-- Value chain panel — visible when a ? button is active -->
        <Grid Grid.Row="4"
              Visibility="{x:Bind ViewModel.ValueChainPanelVisibility, Mode=OneWay}"
              BorderBrush="#2563EB"
              BorderThickness="0,2,0,0"
              Background="#0F172A"
              Padding="12,10">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- Panel header: property name + close button -->
            <Grid Grid.Row="0" Margin="0,0,0,8">
                <TextBlock Text="{x:Bind ViewModel.ValueChainPropertyName, Mode=OneWay}"
                           FontSize="11"
                           FontWeight="SemiBold"
                           Foreground="#60A5FA"
                           VerticalAlignment="Center"/>
                <Button Content="✕"
                        HorizontalAlignment="Right"
                        Command="{x:Bind ViewModel.ClearValueChainCommand}"
                        Style="{StaticResource ChevronButtonStyle}"/>
            </Grid>

            <!-- Chain entries list -->
            <ItemsControl Grid.Row="1"
                          ItemsSource="{x:Bind ViewModel.ActiveValueChain, Mode=OneWay}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:ValueChainEntryViewModel">
                        <Grid Margin="0,2">

                            <!-- Winner row: green left border, blue tint background, "wins" badge -->
                            <Border Background="#1E3A5F"
                                    CornerRadius="4"
                                    BorderBrush="#22C55E"
                                    BorderThickness="3,0,0,0"
                                    Padding="8,5"
                                    Visibility="{x:Bind WinnerBadgeVisibility}">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="150"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <StackPanel Orientation="Horizontal"
                                                VerticalAlignment="Center"
                                                Spacing="4">
                                        <TextBlock Text="{x:Bind Source}"
                                                   FontSize="10"
                                                   FontWeight="SemiBold"
                                                   Foreground="#22C55E"/>
                                        <Border Background="#14532D"
                                                CornerRadius="3"
                                                Padding="3,1">
                                            <TextBlock Text="wins"
                                                       FontSize="9"
                                                       Foreground="#86EFAC"/>
                                        </Border>
                                    </StackPanel>
                                    <TextBlock Grid.Column="1"
                                               Text="{x:Bind Value}"
                                               FontSize="11"
                                               Foreground="#4ADE80"
                                               VerticalAlignment="Center"/>
                                </Grid>
                            </Border>

                            <!-- Overridden row: grey left border, dimmed, strikethrough value -->
                            <Border Background="#1F2937"
                                    CornerRadius="4"
                                    BorderBrush="#374151"
                                    BorderThickness="3,0,0,0"
                                    Padding="8,5"
                                    Opacity="0.6"
                                    Visibility="{x:Bind OverriddenVisibility}">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="150"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Text="{x:Bind Source}"
                                               FontSize="10"
                                               Foreground="#9CA3AF"
                                               VerticalAlignment="Center"/>
                                    <TextBlock Grid.Column="1"
                                               Text="{x:Bind Value}"
                                               FontSize="11"
                                               Foreground="#9CA3AF"
                                               TextDecorations="Strikethrough"
                                               VerticalAlignment="Center"/>
                                </Grid>
                            </Border>

                        </Grid>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </Grid>
```

- [ ] **Step 3: Build Inspector project**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet 2>&1 | tail -6
```

Expected: Build succeeded, 0 error(s).

- [ ] **Step 4: Run all tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows 2>&1 | tail -5
```

Expected: All tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PropertyGridControl.xaml
git commit -m "feat: add value chain panel and ? button to PropertyGridControl"
```

---

## Smoke Test Checklist

After all tasks complete, run the Inspector + SampleApp and verify manually:

- [ ] Select a `Button` → property rows show `?` buttons on rows with non-default values
- [ ] Click `?` on `Background` → panel appears at bottom showing Local / Style / Default sources
- [ ] Clicking `?` again on same row → panel closes (toggle)
- [ ] Click `?` on a different row → panel switches to that property's chain
- [ ] Click ✕ → panel closes
- [ ] Select a different tree node → panel clears (no stale chain from previous selection)
- [ ] `IsTabStop` (or any property at its metadata default) → no `?` button visible
- [ ] Property with active VisualState override → `VisualState (StateName)` entry appears in chain
