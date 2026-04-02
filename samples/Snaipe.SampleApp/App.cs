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
        this.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("ms-appx:///Controls/Themes/Generic.xaml")
        });
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
        _agent = SnaipeAgent.Attach(window);
    }
}
