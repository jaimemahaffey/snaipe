using Microsoft.UI.Xaml.Media;
using Snaipe.Inspector.ViewModels;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class PropertyRowViewModelTests
{
    [Fact]
    public void InitialState_NoError()
    {
        var entry = new PropertyEntry { Name = "Test", Value = "Val", ValueType = "String", ValueKind = "Text", Category = "Common" };
        var vm = new PropertyRowViewModel(entry);

        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void SetError_UpdatesProperties()
    {
        var entry = new PropertyEntry { Name = "Test", Value = "Val", ValueType = "String", ValueKind = "Text", Category = "Common" };
        var vm = new PropertyRowViewModel(entry);

        vm.SetError("Failure message");

        Assert.True(vm.HasError);
        Assert.Equal("Failure message", vm.ErrorMessage);
    }

    [Fact]
    public void ClearError_RestoresProperties()
    {
        var entry = new PropertyEntry { Name = "Test", Value = "Val", ValueType = "String", ValueKind = "Text", Category = "Common" };
        var vm = new PropertyRowViewModel(entry);

        vm.SetError("Failure message");
        vm.ClearError();

        Assert.False(vm.HasError);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void IsCheckedValue_TwoWayBinding()
    {
        var entry = new PropertyEntry { Name = "Test", Value = "False", ValueType = "Boolean", ValueKind = "Boolean", Category = "Common" };
        var vm = new PropertyRowViewModel(entry);

        Assert.False(vm.IsCheckedValue);

        vm.IsCheckedValue = true;
        Assert.Equal("True", vm.EditValue);
        Assert.True(vm.IsCheckedValue);

        vm.EditValue = "False";
        Assert.False(vm.IsCheckedValue);
    }

    [Fact]
    public void NumberValue_TwoWayBinding()
    {
        var entry = new PropertyEntry { Name = "Test", Value = "10", ValueType = "Double", ValueKind = "Number", Category = "Common" };
        var vm = new PropertyRowViewModel(entry);

        Assert.Equal(10.0, vm.NumberValue);

        vm.NumberValue = 25.5;
        Assert.Equal("25.5", vm.EditValue);
        Assert.Equal(25.5, vm.NumberValue);

        vm.EditValue = "42";
        Assert.Equal(42.0, vm.NumberValue);
    }

    [Fact]
    public void JumpToTemplateCommand_WhenSet_IsNonNull()
    {
        var entry = new PropertyEntry
        {
            Name = "ControlTemplate", Category = "Template",
            ValueKind = "String", TemplateOriginKind = "ControlTemplate"
        };
        var cmd = new RelayCommand(() => { });
        var vm = new PropertyRowViewModel(entry, jumpToTemplateCommand: cmd);
        Assert.NotNull(vm.JumpToTemplateCommand);
    }

    [Fact]
    public void JumpToTemplateCommand_WhenNotSet_IsNull()
    {
        var entry = new PropertyEntry { Name = "Width", Category = "Layout", ValueKind = "Number" };
        var vm = new PropertyRowViewModel(entry);
        Assert.Null(vm.JumpToTemplateCommand);
    }
}
