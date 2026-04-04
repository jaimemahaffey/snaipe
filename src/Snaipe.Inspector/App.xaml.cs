using Microsoft.UI.Xaml;

namespace Snaipe.Inspector;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Title = "Snaipe Inspector";
        
#if HAS_UNO_SKIA
        window.AppWindow.SetIcon("icon.ico");
#endif

        window.Activate();
    }
}
