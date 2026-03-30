using Snaipe.Inspector.ViewModels;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class MainViewModelTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void InitialState_RootNodesIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.RootNodes);
    }

    [Fact]
    public void InitialState_PropertyGroupsIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.PropertyGroups);
    }

    [Fact]
    public void InitialState_StatusMessageIsNotNullOrEmpty()
    {
        // StatusMessage reflects the agent scan result at construction time,
        // so we cannot assert a fixed "Ready" string. We verify it is set to
        // something meaningful (either an agent-count message or the "no agents"
        // message).
        var vm = new MainViewModel();
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage));
    }

    [Fact]
    public void SelectedAgent_Null_ConnectCommandCannotExecute()
    {
        var vm = new MainViewModel();
        Assert.Null(vm.SelectedAgent);
        Assert.False(vm.ConnectCommand.CanExecute(null));
    }
}
