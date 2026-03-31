# Property Grid Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the nested `ItemsControl` property grid with a searchable, sortable, flat-row DataGrid-style layout backed by a new `PropertyGridViewModel`.

**Architecture:** `PropertyGridViewModel` owns all filter/sort state and exposes `FilteredProperties` (a flat `ObservableCollection`). `MainViewModel` delegates via `PropertyGrid.Load(rows)` / `PropertyGrid.Clear()`. `PropertyGridControl` binds directly to `PropertyGridViewModel` as its DataContext. The existing `PropertyEditorTemplateSelector` is reused unchanged for the Value cell.

**Tech Stack:** .NET 9, C# 13, Uno Platform 6.5 (Skia), xUnit 2.9, hand-rolled MVVM (no toolkit)

---

## File Map

| File | Action |
|---|---|
| `src/Snaipe.Inspector/ViewModels/RelayCommand.cs` | **Modify** — append `RelayCommand<T>` class |
| `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs` | **New** |
| `src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs` | **Delete** (Task 8) |
| `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` | **Modify** — add `PropertyGrid`, `_propertiesCts`, rewire `OnSelectedNodeChangedAsync`, remove `PropertyGroups` |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml` | **Replace** |
| `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs` | **Modify** — `ViewModel` type, wire `TextChanged` |
| `src/Snaipe.Inspector/MainWindow.xaml` | **Modify** — set `DataContext` on `PropertyGridControl` |
| `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs` | **New** |
| `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs` | **Modify** — remove `PropertyGroups` assertion |

---

## Task 1: Add `RelayCommand<T>`

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/RelayCommand.cs`

- [x] **Step 1: Append `RelayCommand<T>` to the existing file**

Open `src/Snaipe.Inspector/ViewModels/RelayCommand.cs` (currently 20 lines) and append after the closing `}` of `RelayCommand`:

```csharp
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter is T t ? t : default) ?? true;
    public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [x] **Step 2: Build to verify no errors**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet
```

Expected: `Build succeeded. 0 Warning(s). 0 Error(s).`

- [x] **Step 3: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/RelayCommand.cs
git commit -m "feat: add RelayCommand<T> generic command helper"
```

---

## Task 2: `PropertyGridViewModel` — Load and Clear

**Files:**
- Create: `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs`
- Create: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`

- [x] **Step 1: Write the failing tests**

Create `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`:

```csharp
using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class PropertyGridViewModelTests
{
    // Helper: builds a minimal PropertyRowViewModel with no commit action.
    private static PropertyRowViewModel MakeRow(string name, string category, bool readOnly = false) =>
        new(new PropertyEntry { Name = name, Category = category, IsReadOnly = readOnly });

    [Fact]
    public void InitialState_FilteredPropertiesIsEmpty()
    {
        var vm = new PropertyGridViewModel();
        Assert.Empty(vm.FilteredProperties);
    }

    [Fact]
    public void Load_PopulatesFilteredProperties()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout")]);
        Assert.Equal(2, vm.FilteredProperties.Count);
    }

    [Fact]
    public void Load_Twice_ReplacesRows()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout")]);
        vm.Load([MakeRow("Height", "Layout"), MakeRow("Margin", "Layout")]);
        Assert.Equal(2, vm.FilteredProperties.Count);
    }

    [Fact]
    public void Clear_EmptiesFilteredProperties()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout")]);
        vm.Clear();
        Assert.Empty(vm.FilteredProperties);
    }
}
```

- [x] **Step 2: Run tests — expect compile failure**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests"
```

Expected: build error — `PropertyGridViewModel` does not exist.

- [x] **Step 3: Create `PropertyGridViewModel` with Load and Clear**

Create `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs`:

```csharp
using System.Collections.ObjectModel;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyGridViewModel : ViewModelBase
{
    private List<PropertyRowViewModel> _allProperties = [];
    private string _searchText = "";
    private string _activeSortColumn = "Category";
    private bool _sortAscending = true;

    public PropertyGridViewModel()
    {
        SortByCommand = new RelayCommand<string>(SortBy);
    }

    public ObservableCollection<PropertyRowViewModel> FilteredProperties { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                RebuildFilteredProperties();
        }
    }

    public string ActiveSortColumn => _activeSortColumn;
    public bool SortAscending => _sortAscending;

    public RelayCommand<string> SortByCommand { get; }

    public string NameColumnHeader     => "NAME"     + SortIndicator("Name");
    public string TypeColumnHeader     => "TYPE"     + SortIndicator("Type");
    public string CategoryColumnHeader => "CATEGORY" + SortIndicator("Category");
    public string ReadOnlyColumnHeader => "R/O"      + SortIndicator("ReadOnly");

    public void Load(IEnumerable<PropertyRowViewModel> rows)
    {
        _allProperties = rows.ToList();
        RebuildFilteredProperties();
    }

    public void Clear()
    {
        _allProperties = [];
        FilteredProperties.Clear();
    }

    private void SortBy(string? column)
    {
        if (column is null) return;
        if (_activeSortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _activeSortColumn = column;
            _sortAscending = true;
        }
        OnPropertyChanged(nameof(ActiveSortColumn));
        OnPropertyChanged(nameof(SortAscending));
        OnPropertyChanged(nameof(NameColumnHeader));
        OnPropertyChanged(nameof(TypeColumnHeader));
        OnPropertyChanged(nameof(CategoryColumnHeader));
        OnPropertyChanged(nameof(ReadOnlyColumnHeader));
        RebuildFilteredProperties();
    }

    private void RebuildFilteredProperties()
    {
        IEnumerable<PropertyRowViewModel> rows = string.IsNullOrEmpty(_searchText)
            ? _allProperties
            : _allProperties.Where(r => r.Entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        IEnumerable<PropertyRowViewModel> sorted = _activeSortColumn switch
        {
            "Name"     => _sortAscending ? rows.OrderBy(r => r.Entry.Name)           : rows.OrderByDescending(r => r.Entry.Name),
            "Value"    => _sortAscending ? rows.OrderBy(r => r.Entry.Value ?? "")     : rows.OrderByDescending(r => r.Entry.Value ?? ""),
            "Type"     => _sortAscending ? rows.OrderBy(r => r.Entry.ValueType ?? "") : rows.OrderByDescending(r => r.Entry.ValueType ?? ""),
            "ReadOnly" => _sortAscending ? rows.OrderBy(r => r.Entry.IsReadOnly)      : rows.OrderByDescending(r => r.Entry.IsReadOnly),
            _          => _sortAscending
                          ? rows.OrderBy(r => CategoryOrder(r.Entry.Category)).ThenBy(r => r.Entry.Name)
                          : rows.OrderByDescending(r => CategoryOrder(r.Entry.Category)).ThenBy(r => r.Entry.Name),
        };

        FilteredProperties.Clear();
        foreach (var row in sorted)
            FilteredProperties.Add(row);
    }

    private string SortIndicator(string column)
    {
        if (_activeSortColumn != column) return "";
        return _sortAscending ? " ↑" : " ↓";
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Common"        => 0,
        "Layout"        => 1,
        "Appearance"    => 2,
        "Data Context"  => 3,
        "Visual States" => 4,
        "Style"         => 5,
        "Template"      => 6,
        _               => 7,
    };
}
```

- [x] **Step 4: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests"
```

Expected: `4 passed, 0 failed`.

- [x] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs
git commit -m "feat: add PropertyGridViewModel with Load/Clear"
```

---

## Task 3: Add Search Filtering

**Files:**
- Modify: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs` — add search tests
- No production code changes needed — `SearchText` and filtering are already implemented

- [ ] **Step 1: Add search tests**

Append to the `PropertyGridViewModelTests` class body:

```csharp
    [Fact]
    public void SearchText_FiltersOnName_CaseInsensitive()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout"), MakeRow("Visibility", "Layout")]);
        vm.SearchText = "wi";
        Assert.Single(vm.FilteredProperties);
        Assert.Equal("Width", vm.FilteredProperties[0].Entry.Name);
    }

    [Fact]
    public void SearchText_MatchesPartialName()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Background", "Appearance"), MakeRow("BackgroundColor", "Appearance"), MakeRow("Foreground", "Appearance")]);
        vm.SearchText = "background";
        Assert.Equal(2, vm.FilteredProperties.Count);
    }

    [Fact]
    public void SearchText_Empty_RestoresAllRows()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout")]);
        vm.SearchText = "Width";
        vm.SearchText = "";
        Assert.Equal(2, vm.FilteredProperties.Count);
    }

    [Fact]
    public void SearchText_NoMatch_EmptiesFilteredProperties()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout")]);
        vm.SearchText = "zzz";
        Assert.Empty(vm.FilteredProperties);
    }
```

- [ ] **Step 2: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests"
```

Expected: `8 passed, 0 failed`.

- [ ] **Step 3: Commit**

```bash
git add tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs
git commit -m "test: verify PropertyGridViewModel search filtering"
```

---

## Task 4: Add Sort Tests

**Files:**
- Modify: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs` — add sort tests

- [ ] **Step 1: Add sort tests**

Append to the `PropertyGridViewModelTests` class body:

```csharp
    [Fact]
    public void DefaultSort_IsCategorizingThenName()
    {
        var vm = new PropertyGridViewModel();
        // Layout (order 1) comes after Appearance (order 2)? No: Layout=1, Appearance=2 so Layout first.
        vm.Load([
            MakeRow("Margin",     "Layout"),
            MakeRow("Background", "Appearance"),
            MakeRow("Width",      "Layout"),
        ]);
        // Expected order: Layout/Margin, Layout/Width, Appearance/Background
        Assert.Equal("Margin",     vm.FilteredProperties[0].Entry.Name);
        Assert.Equal("Width",      vm.FilteredProperties[1].Entry.Name);
        Assert.Equal("Background", vm.FilteredProperties[2].Entry.Name);
    }

    [Fact]
    public void SortByName_Ascending()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout"), MakeRow("Margin", "Layout")]);
        vm.SortByCommand.Execute("Name");
        Assert.Equal("Height", vm.FilteredProperties[0].Entry.Name);
        Assert.Equal("Margin", vm.FilteredProperties[1].Entry.Name);
        Assert.Equal("Width",  vm.FilteredProperties[2].Entry.Name);
    }

    [Fact]
    public void SortByName_Twice_Descending()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Layout"), MakeRow("Margin", "Layout")]);
        vm.SortByCommand.Execute("Name");
        vm.SortByCommand.Execute("Name");
        Assert.Equal("Width",  vm.FilteredProperties[0].Entry.Name);
        Assert.Equal("Margin", vm.FilteredProperties[1].Entry.Name);
        Assert.Equal("Height", vm.FilteredProperties[2].Entry.Name);
    }

    [Fact]
    public void SortByDifferentColumn_ResetToAscending()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([MakeRow("Width", "Layout"), MakeRow("Height", "Appearance")]);
        vm.SortByCommand.Execute("Name");
        vm.SortByCommand.Execute("Name"); // now descending
        vm.SortByCommand.Execute("Type"); // switch column — should reset to ascending
        Assert.True(vm.SortAscending);
        Assert.Equal("Type", vm.ActiveSortColumn);
    }

    [Fact]
    public void SortIndicator_ActiveColumn_ShowsArrow()
    {
        var vm = new PropertyGridViewModel();
        // Default is Category ascending
        Assert.Contains("↑", vm.CategoryColumnHeader);
        Assert.DoesNotContain("↑", vm.NameColumnHeader);
        Assert.DoesNotContain("↓", vm.NameColumnHeader);
    }

    [Fact]
    public void SortIndicator_AfterSortByName_NameShowsUpArrow()
    {
        var vm = new PropertyGridViewModel();
        vm.SortByCommand.Execute("Name");
        Assert.Contains("↑", vm.NameColumnHeader);
        Assert.DoesNotContain("↑", vm.CategoryColumnHeader);
    }

    [Fact]
    public void SortIndicator_AfterSortByNameTwice_NameShowsDownArrow()
    {
        var vm = new PropertyGridViewModel();
        vm.SortByCommand.Execute("Name");
        vm.SortByCommand.Execute("Name");
        Assert.Contains("↓", vm.NameColumnHeader);
    }
```

- [ ] **Step 2: Run tests — expect pass**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows --filter "FullyQualifiedName~PropertyGridViewModelTests"
```

Expected: `15 passed, 0 failed`.

- [ ] **Step 3: Commit**

```bash
git add tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs
git commit -m "test: verify PropertyGridViewModel sort and indicators"
```

---

## Task 5: Update `MainViewModel`

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`

Replace the `PropertyGroups` machinery with `PropertyGridViewModel`. Make these changes:

- [ ] **Step 1: Add `PropertyGrid` property and `_propertiesCts` field**

In the fields section (around line 16), add:
```csharp
private CancellationTokenSource? _propertiesCts;
```

In the collections section (around line 39–41), replace:
```csharp
public ObservableCollection<PropertyGroupViewModel> PropertyGroups { get; } = [];
```
With:
```csharp
public PropertyGridViewModel PropertyGrid { get; } = new();
```

- [ ] **Step 2: Update `ClearSession` (around line 96–102)**

Replace:
```csharp
PropertyGroups.Clear();
```
With:
```csharp
PropertyGrid.Clear();
```

- [ ] **Step 3: Replace `OnSelectedNodeChangedAsync` body**

The method currently runs from approximately line 221 to 264. Replace the entire method with:

```csharp
private async Task OnSelectedNodeChangedAsync(TreeNodeViewModel? node)
{
    _propertiesCts?.Cancel();
    _propertiesCts = new CancellationTokenSource();
    var ct = _propertiesCts.Token;

    PropertyGrid.Clear();
    if (node is null || _state != ConnectionState.Connected) return;

    IsLoadingProperties = true;
    try
    {
        var response = await _client.SendAsync<PropertiesResponse>(
            new GetPropertiesRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = node.Node.Id,
            });

        if (ct.IsCancellationRequested) return;

        var rows = response.Properties
            .Select(prop => new PropertyRowViewModel(prop,
                row => SetPropertyAsync(node.Node.Id, row.Entry.Name, row.EditValue, row)))
            .ToList();

        PropertyGrid.Load(rows);

        _ = SendHighlightAsync(node.Node.Id, show: true);
    }
    catch (OperationCanceledException)
    {
        // Superseded by a newer selection — do nothing.
    }
    catch (IOException ex)
    {
        HandleConnectionLost(ex.Message);
    }
    catch (SnaipeProtocolException ex) when (ex.ErrorCode == ErrorCodes.ElementNotFound)
    {
        StatusMessage = "Element no longer in tree — refresh the tree.";
    }
    finally
    {
        IsLoadingProperties = false;
    }
}
```

- [ ] **Step 4: Remove `CategoryOrder` method**

Delete the `CategoryOrder` private static method (around line 145–150) — it has moved to `PropertyGridViewModel`.

> **Note:** Do NOT remove the `PropertyGroups` property declaration yet. The existing `PropertyGridControl.xaml` still references `ViewModel.PropertyGroups` via `x:Bind` (compile-time checked). Leave the empty property in place — it will be removed in Task 8 after the XAML is replaced.

- [ ] **Step 5: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/MainViewModel.cs
git commit -m "refactor: wire PropertyGridViewModel into MainViewModel, add CTS guard"
```

---

## Task 6: Update `MainViewModelTests`

**Files:**
- Modify: `tests/Snaipe.Inspector.Tests/MainViewModelTests.cs`

- [ ] **Step 1: Replace the `PropertyGroups` test**

The test `InitialState_PropertyGroupsIsEmpty` references the removed `PropertyGroups` property. Replace it:

```csharp
[Fact]
public void InitialState_PropertyGridIsEmpty()
{
    var vm = new MainViewModel();
    Assert.Empty(vm.PropertyGrid.FilteredProperties);
}
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Snaipe.Inspector.Tests/MainViewModelTests.cs
git commit -m "test: update MainViewModelTests to use PropertyGrid instead of PropertyGroups"
```

---

## Task 7: Replace `PropertyGridControl.xaml` and Code-Behind

**Files:**
- Replace: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs`

- [ ] **Step 1: Replace `PropertyGridControl.xaml` entirely**

```xml
<UserControl
    x:Class="Snaipe.Inspector.Controls.PropertyGridControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:Snaipe.Inspector.Controls"
    xmlns:vm="using:Snaipe.Inspector.ViewModels">

    <UserControl.Resources>

        <local:PropertyEditorTemplateSelector x:Key="EditorSelector">
            <local:PropertyEditorTemplateSelector.ReadOnlyTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBlock Text="{x:Bind Entry.Value}"
                               FontSize="12"
                               TextTrimming="CharacterEllipsis"
                               VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.ReadOnlyTemplate>

            <local:PropertyEditorTemplateSelector.TextTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <TextBox Text="{x:Bind EditValue, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                             FontSize="12"
                             BorderBrush="{x:Bind ErrorBorderBrush, Mode=OneWay}"
                             BorderThickness="1"
                             ToolTipService.ToolTip="{Binding ErrorMessage}"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.TextTemplate>

            <local:PropertyEditorTemplateSelector.BooleanTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <CheckBox IsChecked="{x:Bind IsCheckedValue, Mode=TwoWay}"
                              VerticalAlignment="Center"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.BooleanTemplate>

            <local:PropertyEditorTemplateSelector.NumberTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <NumberBox Value="{Binding NumberValue, Mode=TwoWay}"
                               SpinButtonPlacementMode="Compact"
                               FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.NumberTemplate>

            <local:PropertyEditorTemplateSelector.EnumTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <ComboBox ItemsSource="{Binding Entry.EnumValues}"
                              SelectedItem="{Binding EditValue, Mode=TwoWay}"
                              FontSize="12"/>
                </DataTemplate>
            </local:PropertyEditorTemplateSelector.EnumTemplate>
        </local:PropertyEditorTemplateSelector>

        <Style x:Key="ColumnHeaderButtonStyle" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="4,4"/>
            <Setter Property="HorizontalAlignment" Value="Stretch"/>
            <Setter Property="HorizontalContentAlignment" Value="Left"/>
            <Setter Property="FontSize" Value="11"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
        </Style>

    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Search bar -->
        <TextBox x:Name="SearchBox"
                 Grid.Row="0"
                 PlaceholderText="Search properties..."
                 FontSize="12"
                 Margin="4,4,4,2"/>

        <!-- Column headers -->
        <Grid Grid.Row="1"
              Padding="4,2"
              ColumnSpacing="4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="1*"/>
                <ColumnDefinition Width="1.5*"/>
                <ColumnDefinition Width="50"/>
            </Grid.ColumnDefinitions>

            <Button Grid.Column="0"
                    Content="{x:Bind ViewModel.NameColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Name"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <TextBlock Grid.Column="1"
                       Text="VALUE"
                       FontSize="11"
                       FontWeight="SemiBold"
                       Padding="4"
                       VerticalAlignment="Center"/>

            <Button Grid.Column="2"
                    Content="{x:Bind ViewModel.TypeColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Type"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <Button Grid.Column="3"
                    Content="{x:Bind ViewModel.CategoryColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="Category"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>

            <Button Grid.Column="4"
                    Content="{x:Bind ViewModel.ReadOnlyColumnHeader, Mode=OneWay}"
                    Command="{Binding SortByCommand}"
                    CommandParameter="ReadOnly"
                    Style="{StaticResource ColumnHeaderButtonStyle}"/>
        </Grid>

        <!-- Property rows -->
        <ListView Grid.Row="2"
                  ItemsSource="{x:Bind ViewModel.FilteredProperties, Mode=OneWay}"
                  SelectionMode="None">
            <ListView.ItemContainerStyle>
                <Style TargetType="ListViewItem">
                    <Setter Property="Padding" Value="0"/>
                    <Setter Property="MinHeight" Value="0"/>
                    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
                </Style>
            </ListView.ItemContainerStyle>
            <ListView.ItemTemplate>
                <DataTemplate x:DataType="vm:PropertyRowViewModel">
                    <Grid Padding="4,2"
                          ColumnSpacing="4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="2*"/>
                            <ColumnDefinition Width="2*"/>
                            <ColumnDefinition Width="1*"/>
                            <ColumnDefinition Width="1.5*"/>
                            <ColumnDefinition Width="50"/>
                        </Grid.ColumnDefinitions>

                        <TextBlock Grid.Column="0"
                                   Text="{x:Bind Entry.Name}"
                                   FontSize="12"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"
                                   ToolTipService.ToolTip="{x:Bind Entry.Name}"/>

                        <ContentPresenter Grid.Column="1"
                                          Content="{x:Bind}"
                                          ContentTemplateSelector="{StaticResource EditorSelector}"/>

                        <TextBlock Grid.Column="2"
                                   Text="{x:Bind Entry.ValueType}"
                                   FontSize="10"
                                   Opacity="0.6"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"/>

                        <TextBlock Grid.Column="3"
                                   Text="{x:Bind Entry.Category}"
                                   FontSize="10"
                                   Opacity="0.6"
                                   VerticalAlignment="Center"
                                   TextTrimming="CharacterEllipsis"/>

                        <CheckBox Grid.Column="4"
                                  IsChecked="{x:Bind Entry.IsReadOnly}"
                                  IsEnabled="False"
                                  HorizontalAlignment="Center"
                                  VerticalAlignment="Center"/>
                    </Grid>
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

    </Grid>
</UserControl>
```

- [ ] **Step 2: Update `PropertyGridControl.xaml.cs`**

Replace the entire file:

```csharp
// src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    public PropertyGridControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
        SearchBox.TextChanged += OnSearchTextChanged;
    }

    // DataContext is PropertyGridViewModel (set from MainWindow via x:Bind ViewModel.PropertyGrid).
    public PropertyGridViewModel? ViewModel => DataContext as PropertyGridViewModel;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is { } vm)
            vm.SearchText = SearchBox.Text;
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PropertyGridControl.xaml src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
git commit -m "feat: replace PropertyGridControl with searchable sortable DataGrid-style layout"
```

---

## Task 8: Update `MainWindow.xaml`, Remove `PropertyGroups`, Delete `PropertyGroupViewModel`

**Files:**
- Modify: `src/Snaipe.Inspector/MainWindow.xaml`
- Modify: `src/Snaipe.Inspector/ViewModels/MainViewModel.cs` — remove `PropertyGroups` property (safe now that XAML no longer references it)
- Delete: `src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs`

- [ ] **Step 1: Update `MainWindow.xaml`**

Find the line:
```xml
<controls:PropertyGridControl Grid.Row="0"/>
```

Replace with:
```xml
<controls:PropertyGridControl Grid.Row="0"
                               DataContext="{x:Bind ViewModel.PropertyGrid}"/>
```

- [ ] **Step 2: Remove `PropertyGroups` from `MainViewModel`**

In `src/Snaipe.Inspector/ViewModels/MainViewModel.cs`, delete this line (the XAML no longer references it):

```csharp
public ObservableCollection<PropertyGroupViewModel> PropertyGroups { get; } = [];
```

Also remove the `using` or any remaining reference to `PropertyGroupViewModel` if the compiler flags it.

- [ ] **Step 3: Delete `PropertyGroupViewModel.cs`**

```bash
rm src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs
```

- [ ] **Step 4: Build and run all tests**

```bash
dotnet build src/Snaipe.Inspector/Snaipe.Inspector.csproj -f net9.0-windows -v quiet
dotnet test tests/Snaipe.Inspector.Tests/Snaipe.Inspector.Tests.csproj -f net9.0-windows
```

Expected: build succeeded, all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/Snaipe.Inspector/MainWindow.xaml
git rm src/Snaipe.Inspector/ViewModels/PropertyGroupViewModel.cs
git commit -m "feat: wire PropertyGridViewModel as DataContext, remove PropertyGroupViewModel"
```

---

## Smoke Test Checklist

After all tasks complete, launch `Snaipe.SampleApp` and `Snaipe.Inspector` together and verify:

- [ ] Property grid shows all columns: Name, Value, Type, Category, R/O
- [ ] Default sort groups rows by category (Layout rows together, Appearance rows together)
- [ ] Typing in the search box filters rows in real-time by property name
- [ ] Clearing the search box restores all rows
- [ ] Clicking "NAME" column header sorts alphabetically ascending; clicking again reverses
- [ ] Clicking "CATEGORY" header re-sorts by category; sort indicator appears on that column
- [ ] Editing a text property and tabbing away commits the value
- [ ] Read-only properties show a checked checkbox in the R/O column and are not editable
- [ ] Selecting a new element while properties are loading doesn't leave stale rows
