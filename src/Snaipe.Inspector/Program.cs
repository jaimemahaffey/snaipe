using System.Threading.Tasks;
using Uno.UI.Hosting;

namespace Snaipe.Inspector;

public static class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .UseX11()
            .Build();

        await host.RunAsync();
    }
}
