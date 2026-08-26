using System.IO.Compression;
using Trynex.Infrastructure.Updates;

namespace Trynex.Infrastructure.Tests;

public sealed class VersionedLauncherPackageInstallerTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallAsync_ExtractsIntoVersionedDirectory()
    {
        var package = CreatePackage(("Trynex.Launcher.exe", "launcher"), ("data/file.txt", "data"));
        var installer = new VersionedLauncherPackageInstaller();

        var directory = await installer.InstallAsync(
            package,
            Path.Combine(_testDirectory, "versions"),
            "0.3.0-preview.1");

        Assert.Equal("launcher", await File.ReadAllTextAsync(Path.Combine(directory, "Trynex.Launcher.exe")));
        Assert.Equal("data", await File.ReadAllTextAsync(Path.Combine(directory, "data", "file.txt")));
    }

    [Fact]
    public async Task InstallAsync_RejectsPathTraversalAndCleansStagingDirectory()
    {
        var package = CreatePackage(("Trynex.Launcher.exe", "launcher"), ("../escape.txt", "bad"));
        var versions = Path.Combine(_testDirectory, "versions");
        var installer = new VersionedLauncherPackageInstaller();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            installer.InstallAsync(package, versions, "0.3.0-preview.1"));

        Assert.False(File.Exists(Path.Combine(_testDirectory, "escape.txt")));
        Assert.Empty(Directory.GetDirectories(versions));
    }

    [Fact]
    public async Task InstallAsync_RejectsPackageWithoutLauncherExecutable()
    {
        var package = CreatePackage(("readme.txt", "missing executable"));
        var installer = new VersionedLauncherPackageInstaller();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            installer.InstallAsync(package, Path.Combine(_testDirectory, "versions"), "0.3.0-preview.1"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private string CreatePackage(params (string Path, string Content)[] entries)
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, $"{Guid.NewGuid():N}.zip");

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var item in entries)
        {
            var entry = archive.CreateEntry(item.Path);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(item.Content);
        }

        return path;
    }
}
