using Trynex.Core.Updates;
using Trynex.Infrastructure.Updates;

namespace Trynex.Infrastructure.Tests;

public sealed class LauncherVersionRetentionServiceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Cleanup_RetainsActiveRollbackAndOneEmergencyVersion()
    {
        var versions = CreateVersions(
            "0.3.0-preview.1",
            "0.3.0-preview.2",
            "0.3.0-preview.3",
            "0.3.0-preview.4",
            "0.3.0-preview.5",
            "0.3.0-preview.6");
        var state = new LauncherInstallState(
            ActiveVersion: "0.3.0-preview.6",
            PreviousVersion: "0.3.0-preview.5");
        var service = new LauncherVersionRetentionService();

        var deleted = service.Cleanup(versions, state);

        Assert.Equal(
            ["0.3.0-preview.1", "0.3.0-preview.2", "0.3.0-preview.3"],
            deleted.Order(StringComparer.Ordinal));
        Assert.Equal(
            ["0.3.0-preview.4", "0.3.0-preview.5", "0.3.0-preview.6"],
            Directory.GetDirectories(versions)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Cleanup_RemovesQuarantinedVersionAndDoesNotTouchUnknownFolders()
    {
        var versions = CreateVersions(
            "0.3.0-preview.4",
            "0.3.0-preview.5",
            "0.3.0-preview.6",
            "0.3.0-preview.7");
        Directory.CreateDirectory(Path.Combine(versions, "my-notes"));
        var state = new LauncherInstallState(
            ActiveVersion: "0.3.0-preview.6",
            PreviousVersion: "0.3.0-preview.5",
            FailedVersion: "0.3.0-preview.7");
        var service = new LauncherVersionRetentionService();

        var deleted = service.Cleanup(versions, state);

        Assert.Contains("0.3.0-preview.7", deleted);
        Assert.True(Directory.Exists(Path.Combine(versions, "0.3.0-preview.4")));
        Assert.True(Directory.Exists(Path.Combine(versions, "my-notes")));
    }

    [Fact]
    public void Cleanup_RejectsNonDedicatedRoot()
    {
        Directory.CreateDirectory(_testDirectory);
        var service = new LauncherVersionRetentionService();
        var state = new LauncherInstallState(ActiveVersion: "0.3.0-preview.6");

        Assert.Throws<ArgumentException>(() => service.Cleanup(_testDirectory, state));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreateVersions(params string[] versions)
    {
        var root = Path.Combine(_testDirectory, "versions");
        foreach (var version in versions)
        {
            var directory = Path.Combine(root, version);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Trynex.Launcher.exe"), version);
        }

        return root;
    }
}
