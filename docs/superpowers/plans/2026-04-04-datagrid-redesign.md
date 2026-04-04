# Datagrid Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the simulated ListView property grid with a CommunityToolkit `DataGrid` featuring native 3-column alignment, resizable columns, and Category grouping.

**Architecture:** Use `CommunityToolkit.WinUI.DataGrid` for the UI control and `CommunityToolkit.WinUI.Collections` (`AdvancedCollectionView`) in the ViewModel to manage filtering, sorting, and grouping natively.

**Tech Stack:** WinUI 3, Uno Platform, CommunityToolkit.WinUI

---

### Task 1: Add NuGet Dependencies

**Files:**
- Modify: `src/Snaipe.Inspector/Snaipe.Inspector.csproj`

- [ ] **Step 1: Install DataGrid and Collections packages**

Run the following commands to add the necessary CommunityToolkit packages (matching the existing Sizers version):

```bash
dotnet add src/Snaipe.Inspector/Snaipe.Inspector.csproj package CommunityToolkit.WinUI.DataGrid --version 8.1.240916
dotnet add src/Snaipe.Inspector/Snaipe.Inspector.csproj package CommunityToolkit.WinUI.Collections --version 8.1.240916
```

- [ ] **Step 2: Commit**

```bash
git add src/Snaipe.Inspector/Snaipe.Inspector.csproj
git commit -m "build: add CommunityToolkit DataGrid and Collections packages"
```

### Task 2: Update PropertyGridViewModel

**Files:**
- Modify: `src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs`

- [ ] **Step 1: Add using directives and replace `FilteredProperties` type**

Modify `PropertyGridViewModel.cs` to use `AdvancedCollectionView`. Remove the custom sorting logic and use the collection view's native capabilities.

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.WinUI.Collections;

namespace Snaipe.Inspector.ViewModels;

public sealed class PropertyGridViewModel : ViewModelBase
{
    private List<PropertyRowViewModel> _allProperties = [];
    private string _searchText = "";
    
    private PropertyRowViewModel? _activeChainRow;
    private ValueChainEntryViewModel[]? _activeValueChain;
    private string? _valueChainPropertyName;

    public PropertyGridViewModel()
    {
        ClearValueChainCommand = new RelayCommand(ClearValueChain);
        FilteredProperties = new AdvancedCollectionView(_allProperties, true);
        
        // Setup filtering
        FilteredProperties.Filter = x => 
        {
            if (string.IsNullOrEmpty(_searchText)) return true;
            var row = (PropertyRowViewModel)x;
            return row.Entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
        };

        // Setup grouping by Category
        FilteredProperties.GroupDescriptions.Add(new PropertyGroupDescription("Entry.Category"));

        // Setup default sort (Category then Name)
        FilteredProperties.SortDescriptions.Add(new SortDescription("Entry.Category", SortDirection.Ascending));
        FilteredProperties.SortDescriptions.Add(new SortDescription("Entry.Name", SortDirection.Ascending));
    }

    public AdvancedCollectionView FilteredProperties { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                FilteredProperties.RefreshFilter();
        }
    }

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

    public void ShowValueChain(PropertyRowViewModel row)
    {
        if (row.ValueChain is null) return;

        if (ReferenceEquals(_activeChainRow, row) && ActiveValueChain is not null)
        {
            ClearValueChain();
            return;
        }

        _activeChainRow = row;
        ActiveValueChain = row.ValueChain.ToArray();
        ValueChainPropertyName = $"{row.Entry.Name} — value chain";
    }

    public void ClearValueChain()
    {
        _activeChainRow = null;
        ActiveValueChain = null;
        ValueChainPropertyName = null;
    }

    public void Load(IEnumerable<PropertyRowViewModel> rows)
    {
        _allProperties.Clear();
        _allProperties.AddRange(rows);
        FilteredProperties.Refresh();
    }

    public void Clear()
    {
        _allProperties.Clear();
        FilteredProperties.Refresh();
        ClearValueChain();
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build src/Snaipe.Inspector`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/Snaipe.Inspector/ViewModels/PropertyGridViewModel.cs
git commit -m "refactor: update PropertyGridViewModel to use AdvancedCollectionView"
```

### Task 3: Update PropertyGridViewModelTests

**Files:**
- Modify: `tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs`

- [ ] **Step 1: Write test to verify Load and Filtering**

Add these tests to `PropertyGridViewModelTests.cs` to verify the new `AdvancedCollectionView` integration:

```csharp
    [Fact]
    public void Load_PopulatesFilteredProperties()
    {
        var grid = new PropertyGridViewModel();
        var row = MakeRowWithChain("Background");
        grid.Load(new[] { row });

        Assert.Single(grid.FilteredProperties);
    }

    [Fact]
    public void SearchText_FiltersOnName_CaseInsensitive()
    {
        var grid = new PropertyGridViewModel();
        grid.Load(new[] { MakeRowWithChain("Background"), MakeRowWithChain("Foreground") });

        grid.SearchText = "ground";
        Assert.Equal(2, grid.FilteredProperties.Count);

        grid.SearchText = "back";
        Assert.Single(grid.FilteredProperties);
        var first = (PropertyRowViewModel)grid.FilteredProperties[0];
        Assert.Equal("Background", first.Entry.Name);
    }
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Snaipe.Inspector.Tests`
Expected: Tests PASS.

- [ ] **Step 3: Commit**

```bash
git add tests/Snaipe.Inspector.Tests/PropertyGridViewModelTests.cs
git commit -m "test: update PropertyGridViewModelTests for AdvancedCollectionView"
```

### Task 4: Replace ListView with DataGrid

**Files:**
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml`
- Modify: `src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs`

- [ ] **Step 1: Remove ListView and implement DataGrid**

In `PropertyGridControl.xaml`, add the namespace `xmlns:controls="using:CommunityToolkit.WinUI.UI.Controls"` at the root. 

Replace the entire `<ListView ...>` (including its `Header` and `ItemTemplate`) with this `<controls:DataGrid>` block:

```xml
        <!-- Property rows -->
        <controls:DataGrid Grid.Row="2"
                           ItemsSource="{x:Bind ViewModel.FilteredProperties, Mode=OneWay}"
                           AutoGenerateColumns="False"
                           CanUserSortColumns="True"
                           CanUserReorderColumns="False"
                           CanUserResizeColumns="True"
                           HeadersVisibility="Column"
                           GridLinesVisibility="Horizontal"
                           SelectionMode="Single"
                           RowDetailsVisibilityMode="Collapsed">
            <controls:DataGrid.Columns>
                <!-- Name Column (Includes drill-down controls and lock icon) -->
                <controls:DataGridTemplateColumn Header="NAME" Width="2*">
                    <controls:DataGridTemplateColumn.CellTemplate>
                        <DataTemplate x:DataType="vm:PropertyRowViewModel">
                            <Grid Padding="4,2">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                
                                <TextBlock Grid.Column="0" 
                                           Text="🔒" 
                                           FontSize="10" 
                                           Foreground="#9CA3AF" 
                                           VerticalAlignment="Center" 
                                           Margin="0,0,4,0"
                                           Visibility="{x:Bind Entry.IsReadOnly, Converter={StaticResource BoolToVisConverter}}"/>
                                           
                                <TextBlock Grid.Column="1"
                                           Text="{x:Bind Entry.Name}"
                                           FontSize="12"
                                           VerticalAlignment="Center"
                                           TextTrimming="CharacterEllipsis"
                                           Opacity="{x:Bind Entry.IsReadOnly, Converter={StaticResource BoolToOpacityConverter}}"/>
                                           
                                <StackPanel Grid.Column="2" 
                                            Orientation="Horizontal"
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
                            </Grid>
                        </DataTemplate>
                    </controls:DataGridTemplateColumn.CellTemplate>
                </controls:DataGridTemplateColumn>

                <!-- Value Column -->
                <controls:DataGridTemplateColumn Header="VALUE" Width="2*">
                    <controls:DataGridTemplateColumn.CellTemplate>
                        <DataTemplate x:DataType="vm:PropertyRowViewModel">
                            <ContentPresenter Content="{x:Bind}"
                                              ContentTemplateSelector="{StaticResource EditorSelector}"
                                              Margin="4,2"/>
                        </DataTemplate>
                    </controls:DataGridTemplateColumn.CellTemplate>
                </controls:DataGridTemplateColumn>

                <!-- Type Column -->
                <controls:DataGridTextColumn Header="TYPE" Width="1*" Binding="{Binding Entry.ValueType}" FontSize="10" Foreground="#9CA3AF" />
            </controls:DataGrid.Columns>
        </controls:DataGrid>
```

- [ ] **Step 2: Add converters to Resources**

In the `<UserControl.Resources>` section of `PropertyGridControl.xaml`, add these converters:

```xml
        <local:BoolToVisibilityConverter x:Key="BoolToVisConverter"/>
        <local:BoolToOpacityConverter x:Key="BoolToOpacityConverter"/>
```

- [ ] **Step 3: Create Converters in code-behind**

Add the converters to `PropertyGridControl.xaml.cs` (or a dedicated `Converters.cs` file in Controls folder, but doing it in the code-behind file for simplicity is fine if they are local):

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Snaipe.Inspector.Controls;

public sealed partial class PropertyGridControl : UserControl
{
    public PropertyGridControl()
    {
        this.InitializeComponent();
    }

    public ViewModels.PropertyGridViewModel ViewModel => DataContext as ViewModels.PropertyGridViewModel;
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value is bool b && b) ? 0.6 : 1.0;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build src/Snaipe.Inspector`
Expected: Build succeeds without XAML errors.

- [ ] **Step 5: Commit**

```bash
git add src/Snaipe.Inspector/Controls/PropertyGridControl.xaml src/Snaipe.Inspector/Controls/PropertyGridControl.xaml.cs
git commit -m "feat: migrate property grid to CommunityToolkit DataGrid"
```
