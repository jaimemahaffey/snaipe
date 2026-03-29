using Microsoft.UI.Xaml;
using Snaipe.Agent;

namespace Snaipe.SampleApp;

public class App : Application
{
    private SnaipeAgent? _agent;

    public App()
    {
        this.Resources.MergedDictionaries.Add(
            new Microsoft.UI.Xaml.Controls.XamlControlsResources());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();

        // Attach the inspector agent AFTER the window is activated
        // so the Win32 message pump is fully initialized first.
        _agent = SnaipeAgent.Attach(window);
    }
}
