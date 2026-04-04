using Microsoft.UI.Xaml;

namespace Snaipe.Inspector;

public class App : Application
{
    public App()
    {
        this.Resources.MergedDictionaries.Add(
            new Microsoft.UI.Xaml.Controls.XamlControlsResources());
        this.Resources.MergedDictionaries.Add(
            new Uno.Toolkit.UI.ToolkitResources());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Activate();
    }
}
