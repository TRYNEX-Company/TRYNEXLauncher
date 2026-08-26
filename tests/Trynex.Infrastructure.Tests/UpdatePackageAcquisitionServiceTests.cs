using Trynex.Core.Abstractions;
using Trynex.Core.Updates;
using Trynex.Infrastructure.Updates;

namespace Trynex.Infrastructure.Tests;

public sealed class UpdatePackageAcquisitionServiceTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AcquireAsync_DownloadsOnlyFromTrustedOriginAndReturnsVerifiedPackage()
    {
        var manifest = CreateManifest();
        var downloader = new FakeDownloader([1, 2, 3]);
        var service = CreateService(downloader, manifest.PackageSha256);

        var packagePath = await service.AcquireAsync(
            new Uri("https://updates.trynex.test/"),
            manifest,
            _testDirectory);

        Assert.Equal(
            new Uri("https://updates.trynex.test/launcher/preview/package.zip"),
            downloader.Source);
        Assert.True(File.Exists(packagePath));
    }

    [Fact]
    public async Task AcquireAsync_DeletesPackageWhenSha256DoesNotMatch()
    {
        var manifest = CreateManifest();
        var service = CreateService(new FakeDownloader([1, 2, 3]), new string('b', 64));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.AcquireAsync(
            new Uri("https://updates.trynex.test/"),
            manifest,
            _testDirectory));

        Assert.Empty(Directory.GetFiles(_testDirectory, "*.zip"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private static UpdatePackageAcquisitionService CreateService(
        IUpdatePackageDownloader downloader,
        string computedHash)
    {
        return new(
            downloader,
            new FakeHashService(computedHash),
            new SignedLauncherManifestVerifier(
                new AcceptingSignatureVerifier(),
                new LauncherUpdateManifestValidator()));
    }

    private static LauncherUpdateManifest CreateManifest()
    {
        return new(
            1,
            "TRYNEX.Launcher",
            "preview",
            "0.3.0-preview.1",
            DateTimeOffset.Parse("2026-08-08T06:00:00Z"),
            "launcher/preview/package.zip",
            3,
            new string('a', 64),
            Convert.ToBase64String([1, 2, 3]));
    }

    private sealed class FakeDownloader(byte[] content) : IUpdatePackageDownloader
    {
        public Uri? Source { get; private set; }

        public async Task DownloadAsync(
            Uri source,
            string destinationPath,
            long expectedSize,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Source = source;
            await File.WriteAllBytesAsync(destinationPath, content, cancellationToken);
        }
    }

    private sealed class FakeHashService(string hash) : IFileHashService
    {
        public Task<string> ComputeSha256Async(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(hash);
        }
    }

    private sealed class AcceptingSignatureVerifier : IManifestSignatureVerifier
    {
        public bool Verify(ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> signature) => true;
    }
}
