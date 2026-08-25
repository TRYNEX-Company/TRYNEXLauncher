using System.Threading;
using System.Windows;

namespace Trynex.Bootstrapper;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\TRYNEX.Bootstrapper";

    [STAThread]
    private static int Main()
    {
        using var singleInstance = new Mutex(true, SingleInstanceMutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            return 0;
        }

        var paths = BootstrapperPaths.CreateDefault();
        var logger = new BootstrapperLogger(paths.LogPath);
        var bootstrapper = new BootstrapperApplication(paths, logger);

        var application = new Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown
        };
        var window = new BootstrapperWindow(bootstrapper, logger);

        return application.Run(window);
    }
}
