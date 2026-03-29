using System.IO;
using Uno.UI.Hosting;

namespace Snaipe.SampleApp;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var logFile = Path.Combine(Path.GetTempPath(), "snaipe-sampleapp-crash.log");

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                File.AppendAllText(logFile,
                    $"[{DateTime.Now:HH:mm:ss.fff}] UNHANDLED: {e.ExceptionObject}\n");
            };

            var host = UnoPlatformHostBuilder.Create()
                .App(() => new App())
                .UseX11()
                .UseWin32()
                .Build();

            host.Run();
        }
        catch (Exception ex)
        {
            File.AppendAllText(logFile,
                $"[{DateTime.Now:HH:mm:ss.fff}] FATAL: {ex}\n");
            throw;
        }
    }
}
