using Trynex.Core.Abstractions;
using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class FileIntegrityVerifierTests
{
    private const string ExpectedHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public async Task VerifyAsync_ReturnsValid_ForMatchingFile()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var filePath = Path.Combine(testDirectory, "game.bin");
            await File.WriteAllTextAsync(filePath, "abc");
            var manifest = new UpdateManifest(
                "1.0.0",
                DateTimeOffset.UtcNow,
                [new FileManifestEntry("game.bin", 3, ExpectedHash)]);
            var verifier = new FileIntegrityVerifier(new StubHashService(ExpectedHash), new ManifestValidator());

            var results = await verifier.VerifyAsync(testDirectory, manifest);

            var result = Assert.Single(results);
            Assert.Equal(FileIntegrityStatus.Valid, result.Status);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_ReturnsMissing_WithoutHashingAbsentFile()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var hashService = new CountingHashService();
            var manifest = new UpdateManifest(
                "1.0.0",
                DateTimeOffset.UtcNow,
                [new FileManifestEntry("missing.bin", 3, ExpectedHash)]);
            var verifier = new FileIntegrityVerifier(hashService, new ManifestValidator());

            var results = await verifier.VerifyAsync(testDirectory, manifest);

            Assert.Equal(FileIntegrityStatus.Missing, Assert.Single(results).Status);
            Assert.Equal(0, hashService.CallCount);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_RejectsInvalidManifestBeforeReadingFiles()
    {
        var manifest = new UpdateManifest(
            "1.0.0",
            DateTimeOffset.UtcNow,
            [new FileManifestEntry("../escape.bin", 3, ExpectedHash)]);
        var verifier = new FileIntegrityVerifier(new StubHashService(ExpectedHash), new ManifestValidator());

        await Assert.ThrowsAsync<ArgumentException>(() => verifier.VerifyAsync(Path.GetTempPath(), manifest));
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "trynex-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHashService(string hash) : IFileHashService
    {
        public Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(hash);
    }

    private sealed class CountingHashService : IFileHashService
    {
        public int CallCount { get; private set; }

        public Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ExpectedHash);
        }
    }
}
