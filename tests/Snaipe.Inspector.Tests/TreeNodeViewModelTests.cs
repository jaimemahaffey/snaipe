using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using System.ComponentModel;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class TreeNodeViewModelTests
{
    private static ElementNode MakeNode(string id, string typeName, string? name = null,
        List<ElementNode>? children = null) => new()
    {
        Id = id, TypeName = typeName, Name = name,
        Children = children ?? [],
    };

    [Fact]
    public void DisplayName_WithName_IncludesQuotedName()
    {
        var node = MakeNode("1", "Button", "SubmitBtn");
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("Button \"SubmitBtn\"", vm.DisplayName);
    }

    [Fact]
    public void DisplayName_WithoutName_ShowsTypeOnly()
    {
        var node = MakeNode("1", "StackPanel");
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("StackPanel", vm.DisplayName);
    }

    [Fact]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Grid"));
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsExpanded = true;

        Assert.Contains(nameof(TreeNodeViewModel.IsExpanded), raised);
    }

    [Fact]
    public void Children_AreEmptyByDefault()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Border"));
        Assert.Empty(vm.Children);
    }
}
