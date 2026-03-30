// src/Snaipe.Inspector/Controls/ConnectionBarControl.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Snaipe.Inspector.ViewModels;

namespace Snaipe.Inspector.Controls;

public sealed partial class ConnectionBarControl : UserControl
{
    public ConnectionBarControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public MainViewModel ViewModel => (MainViewModel)DataContext;

    // Switches label based on connection state for the Connect button.
    public string ConnectButtonLabel =>
        ViewModel?.IsConnected == true ? "Connected" : "Connect";
}
