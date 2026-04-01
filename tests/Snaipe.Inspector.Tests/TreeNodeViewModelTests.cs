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

    [Fact]
    public void TemplateLabel_ControlTemplate_ReturnsOriginString()
    {
        var node = new ElementNode { Id = "1", TypeName = "Border", TemplateOrigin = "ControlTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ControlTemplate", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_ItemTemplateWithCount_ReturnsCountSuffix()
    {
        var node = new ElementNode { Id = "1", TypeName = "Grid", TemplateOrigin = "ItemTemplate", TemplateInstanceCount = 5 };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal("ItemTemplate ×5", vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabel_Null_ReturnsNull()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Null(vm.TemplateLabel);
    }

    [Fact]
    public void TemplateLabelVisibility_NullOrigin_IsCollapsed()
    {
        var vm = new TreeNodeViewModel(MakeNode("1", "Button"));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, vm.TemplateLabelVisibility);
    }

    [Fact]
    public void TemplateLabelVisibility_WithOrigin_IsVisible()
    {
        var node = new ElementNode { Id = "1", TypeName = "Border", TemplateOrigin = "ContentTemplate" };
        var vm = new TreeNodeViewModel(node);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, vm.TemplateLabelVisibility);
    }
}
