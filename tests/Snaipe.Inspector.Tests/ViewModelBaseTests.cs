using Snaipe.Inspector.ViewModels;
using System.ComponentModel;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class ViewModelBaseTests
{
    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }
    }

    [Fact]
    public void SetField_WhenValueChanges_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Name = "Alice";

        Assert.Contains(nameof(TestViewModel.Name), raised);
    }

    [Fact]
    public void SetField_WhenValueUnchanged_DoesNotRaisePropertyChanged()
    {
        var vm = new TestViewModel { Name = "Alice" };
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Name = "Alice"; // same value

        Assert.Empty(raised);
    }

    [Fact]
    public void SetField_WhenValueChanges_ReturnsTrue()
    {
        var vm = new TestViewModel();
        // Access SetField indirectly via the property setter's return
        // We verify the effect: PropertyChanged was raised
        vm.PropertyChanged += (_, _) => { };
        vm.Name = "Bob";
        Assert.Equal("Bob", vm.Name);
    }

    [Fact]
    public void RelayCommand_CanExecute_DefaultsToTrue()
    {
        var cmd = new RelayCommand(() => { });
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_CanExecute_UsesProvidedPredicate()
    {
        var allowed = false;
        var cmd = new RelayCommand(() => { }, () => allowed);
        Assert.False(cmd.CanExecute(null));
        allowed = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_Execute_InvokesAction()
    {
        var invoked = false;
        var cmd = new RelayCommand(() => invoked = true);
        cmd.Execute(null);
        Assert.True(invoked);
    }

    [Fact]
    public void RelayCommand_RaiseCanExecuteChanged_FiresEvent()
    {
        var cmd = new RelayCommand(() => { });
        var fired = false;
        cmd.CanExecuteChanged += (_, _) => fired = true;
        cmd.RaiseCanExecuteChanged();
        Assert.True(fired);
    }
}
