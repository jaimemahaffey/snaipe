using Uno.UI.Hosting;

namespace Snaipe.Inspector;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseWin32()
            .Build();

        host.Run();
    }
}
