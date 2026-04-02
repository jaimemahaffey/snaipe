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
                var msg = $"[{DateTime.Now:HH:mm:ss.fff}] UNHANDLED: {e.ExceptionObject}\n";
                File.AppendAllText(logFile, msg);
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
            var msg = $"[{DateTime.Now:HH:mm:ss.fff}] FATAL: {ex}\n";
            File.AppendAllText(logFile, msg);
            throw;
        }
    }
}
