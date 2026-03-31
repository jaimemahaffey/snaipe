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
    public void InitialState_PropertyGridIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.PropertyGrid.FilteredProperties);
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

    [Fact]
    public void InitialState_BreadcrumbIsEmpty()
    {
        var vm = new MainViewModel();
        Assert.Empty(vm.Breadcrumb);
    }

    [Fact]
    public void DrillInto_PushesBreadcrumbSegment()
    {
        var vm = new MainViewModel();
        // Seed breadcrumb as OnSelectedNodeChangedAsync would at root.
        vm.Breadcrumb.Add(new BreadcrumbSegment("Button", []));

        var row = new PropertyRowViewModel(new Snaipe.Protocol.PropertyEntry
        {
            Name = "DataContext", Category = "Data Context", IsObjectValued = true
        });
        vm.DrillIntoCommand.Execute(row);

        Assert.Equal(2, vm.Breadcrumb.Count);
        Assert.Equal("DataContext", vm.Breadcrumb[1].Label);
        Assert.Equal(new[] { "DataContext" }, vm.Breadcrumb[1].Path);
    }

    [Fact]
    public void DrillInto_NestedLevel_BuildsPathCorrectly()
    {
        var vm = new MainViewModel();
        vm.Breadcrumb.Add(new BreadcrumbSegment("Button", []));
        vm.Breadcrumb.Add(new BreadcrumbSegment("DataContext", ["DataContext"]));

        var row = new PropertyRowViewModel(new Snaipe.Protocol.PropertyEntry
        {
            Name = "Address", Category = "Properties", IsObjectValued = true
        });
        vm.DrillIntoCommand.Execute(row);

        Assert.Equal(3, vm.Breadcrumb.Count);
        Assert.Equal(new[] { "DataContext", "Address" }, vm.Breadcrumb[2].Path);
    }

    [Fact]
    public void NavigateToBreadcrumb_PopsToClickedCrumb()
    {
        var vm = new MainViewModel();
        var root = new BreadcrumbSegment("Button", []);
        var dc   = new BreadcrumbSegment("DataContext", ["DataContext"]);
        var addr = new BreadcrumbSegment("Address", ["DataContext", "Address"]);
        vm.Breadcrumb.Add(root);
        vm.Breadcrumb.Add(dc);
        vm.Breadcrumb.Add(addr);

        vm.NavigateToBreadcrumbCommand.Execute(dc);

        Assert.Equal(2, vm.Breadcrumb.Count);
        Assert.Equal("DataContext", vm.Breadcrumb[1].Label);
    }

    [Fact]
    public void SelectedNode_SetToNull_ClearsBreadcrumb()
    {
        var vm = new MainViewModel();
        // Set a non-null node first so the setter fires when we set null.
        var node = new TreeNodeViewModel(
            new Snaipe.Protocol.ElementNode { Id = "1", TypeName = "Button" });
        vm.SelectedNode = node;
        vm.Breadcrumb.Add(new BreadcrumbSegment("DataContext", ["DataContext"]));

        vm.SelectedNode = null;

        Assert.Empty(vm.Breadcrumb);
    }
}
