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
    public void Load_PopulatesFilteredProperties()
    {
        var grid = new PropertyGridViewModel();
        var row = MakeRowWithChain("Background");
        grid.Load(new[] { row });

        Assert.Single(grid.FilteredProperties); // 1 group
        Assert.Single(grid.FilteredProperties[0]); // 1 item
    }

    [Fact]
    public void SearchText_FiltersOnName_CaseInsensitive()
    {
        var grid = new PropertyGridViewModel();
        grid.Load(new[] { MakeRowWithChain("Background"), MakeRowWithChain("Foreground") });

        grid.SearchText = "ground";
        Assert.Single(grid.FilteredProperties); // Both in "Appearance" group
        Assert.Equal(2, grid.FilteredProperties[0].Count);

        grid.SearchText = "back";
        Assert.Single(grid.FilteredProperties);
        Assert.Single(grid.FilteredProperties[0]);
        var first = grid.FilteredProperties[0][0];
        Assert.Equal("Background", first.Entry.Name);
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
