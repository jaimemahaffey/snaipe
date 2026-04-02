using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class MainViewModelPickTests
{
    [Fact]
    public void OnAgentEventReceived_ElementUnderCursorEvent_SelectsMatchingNode()
    {
        var vm = new MainViewModel();

        var child = new TreeNodeViewModel(new ElementNode { Id = "child-id", TypeName = "Button" });
        var parent = new TreeNodeViewModel(new ElementNode { Id = "parent-id", TypeName = "Grid" });
        parent.Children.Add(child);
        vm.RootNodes.Add(parent);

        vm.OnAgentEventReceived(new ElementUnderCursorEvent
        {
            MessageId = "test",
            ElementId = "child-id",
            TypeName = "Button"
        });

        Assert.Equal(child, vm.SelectedNode);
        Assert.Equal("Picking: Button", vm.StatusMessage);
    }

    [Fact]
    public void OnAgentEventReceived_PickModeActiveEvent_True_UpdatesStatusMessage()
    {
        var vm = new MainViewModel();

        vm.OnAgentEventReceived(new PickModeActiveEvent { MessageId = "test", Active = true });

        Assert.Equal("Pick mode active — Ctrl+Shift + hover to select", vm.StatusMessage);
    }

    [Fact]
    public void OnAgentEventReceived_PickModeActiveEvent_False_RestoresConnectedMessage()
    {
        var vm = new MainViewModel();

        vm.OnAgentEventReceived(new PickModeActiveEvent { MessageId = "test", Active = false });

        // Disconnected state has no selected agent, so message shows "Disconnected."
        Assert.DoesNotContain("Pick mode", vm.StatusMessage);
    }

    [Fact]
    public void OnAgentEventReceived_ElementIdNotFound_DoesNotChangeSelectedNode()
    {
        var vm = new MainViewModel();
        var node = new TreeNodeViewModel(new ElementNode { Id = "known-id", TypeName = "Grid" });
        vm.RootNodes.Add(node);

        vm.OnAgentEventReceived(new ElementUnderCursorEvent
        {
            MessageId = "test",
            ElementId = "unknown-id",
            TypeName = "Button"
        });

        Assert.Null(vm.SelectedNode);
    }
}
