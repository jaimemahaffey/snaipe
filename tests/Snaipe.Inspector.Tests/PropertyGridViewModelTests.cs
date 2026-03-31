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

    [Fact]
    public void DefaultSort_IsCategorizingThenName()
    {
        var vm = new PropertyGridViewModel();
        vm.Load([
            MakeRow("Margin",     "Layout"),
            MakeRow("Background", "Appearance"),
            MakeRow("Width",      "Layout"),
        ]);
        // Layout (order 1) before Appearance (order 2)
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
}
