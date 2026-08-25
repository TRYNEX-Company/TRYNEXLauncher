using System.IO;

namespace Trynex.Bootstrapper;

internal sealed record BootstrapperPaths(
    string RootDirectory,
    string VersionsDirectory,
    string DownloadsDirectory,
    string HealthDirectory,
    string StatePath,
    string LogPath)
{
    public static BootstrapperPaths CreateDefault()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(localAppData, "TRYNEX", "Launcher");

        return new(
            root,
            Path.Combine(root, "versions"),
            Path.Combine(root, "downloads"),
            Path.Combine(root, "health"),
            Path.Combine(root, "state.json"),
            Path.Combine(root, "logs", "bootstrapper.log"));
    }
}
