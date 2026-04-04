using Microsoft.UI.Xaml;
using Snaipe.Agent;
using System.Linq;

namespace Snaipe.SampleApp;

public partial class App : Application
{
    private SnaipeAgent? _agent;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        window.Title = "ASCII Studio (Snaipe Sample)";
        
#if HAS_UNO_SKIA
        window.AppWindow.SetIcon("icon.ico");
#endif

        window.Activate();
        _agent = SnaipeAgent.Attach(window);

        // Automated UI Test logic
        var argsList = Environment.GetCommandLineArgs();
        if (argsList.Contains("--test-open"))
        {
            Task.Run(async () =>
            {
                await Task.Delay(2000); // Wait for app to stabilize
                window.DispatcherQueue.TryEnqueue(() =>
                {
                    if (window.Content is FrameworkElement root)
                    {
                        var vm = root.DataContext as ViewModels.ShellViewModel;
                        string? testPath = null;
                        for (int i = 0; i < argsList.Length - 1; i++)
                        {
                            if (argsList[i] == "--test-open-path")
                            {
                                testPath = argsList[i+1];
                                break;
                            }
                        }

                        if (testPath != null && vm?.LoadImageDirectCommand.CanExecute(testPath) == true)
                        {
                            Console.WriteLine($"[Test] Simulating direct load of {testPath}...");
                            vm.LoadImageDirectCommand.Execute(testPath);
                        }
                        else if (vm?.OpenImageCommand.CanExecute(null) == true)
                        {
                            Console.WriteLine("[Test] Simulating click on Open button...");
                            vm.OpenImageCommand.Execute(null);
                        }
                    }
                });
            });
        }
    }
}
