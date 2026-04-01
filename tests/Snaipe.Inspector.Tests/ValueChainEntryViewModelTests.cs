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
