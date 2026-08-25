using Trynex.Core.Abstractions;
using Trynex.Core.Projects;
using Trynex.Core.Updates;
using Trynex.Infrastructure.Files;
using Trynex.Infrastructure.Projects;

namespace Trynex.Infrastructure.Tests;

public sealed class ProjectContentInstallationServiceTests : IDisposable
{
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SynchronizeAsync_DownloadsVerifiesAndReusesValidContent()
    {
        Directory.CreateDirectory(_testDirectory);
        var downloader = new FakeDownloader([97, 98, 99]);
        var service = new ProjectContentInstallationService(downloader, new Sha256FileHashService());
        var project = CreateProject(AbcHash);

        var first = await service.SynchronizeAsync(
            new Uri("https://cdn.trynex.test/"),
            project,
            _testDirectory);
        var second = await service.SynchronizeAsync(
            new Uri("https://cdn.trynex.test/"),
            project,
            _testDirectory);

        Assert.Equal(1, first.DownloadedFiles);
        Assert.Equal(1, second.AlreadyValidFiles);
        Assert.Equal(1, downloader.CallCount);
        Assert.Equal(
            new Uri("https://cdn.trynex.test/projects/mr-project/1.0.0/package.bin"),
            downloader.LastSource);
        Assert.Equal(
            new byte[] { 97, 98, 99 },
            await File.ReadAllBytesAsync(Path.Combine(_testDirectory, "mr-project", "addons", "package.bin")));
    }

    [Fact]
    public async Task SynchronizeAsync_DeletesFileWhenDownloadedHashDoesNotMatch()
    {
        Directory.CreateDirectory(_testDirectory);
        var service = new ProjectContentInstallationService(
            new FakeDownloader([1, 2, 3]),
            new Sha256FileHashService());
        var project = CreateProject(AbcHash);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SynchronizeAsync(
            new Uri("https://cdn.trynex.test/"),
            project,
            _testDirectory));

        Assert.False(File.Exists(Path.Combine(_testDirectory, "mr-project", "addons", "package.bin")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private static ProjectManifest CreateProject(string hash) => new(
        1,
        "mr-project",
        "1.0.0",
        GamePlatform.ArmaReforger,
        Text("MR PROJECT"),
        Text("Description"),
        Text("READY"),
        "#68D9FA",
        "mr-project.png",
        "projects/mr-project/1.0.0/",
        new ProjectLaunchProfile("1874880", Arguments: []),
        [new ProjectFileEntry("addons/package.bin", "package.bin", 3, hash)]);

    private static LocalizedProjectText Text(string value) => new(new Dictionary<string, string>
    {
        ["en-US"] = value
    });

    private sealed class FakeDownloader(byte[] content) : IUpdatePackageDownloader
    {
        public int CallCount { get; private set; }

        public Uri? LastSource { get; private set; }

        public async Task DownloadAsync(
            Uri source,
            string destinationPath,
            long expectedSize,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastSource = source;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, content, cancellationToken);
            progress?.Report(new(content.LongLength, expectedSize, content.LongLength));
        }
    }
}
