using System.Net;
using System.Net.Http.Headers;
using Trynex.Infrastructure.Networking;

namespace Trynex.Infrastructure.Tests;

public sealed class ResumableUpdatePackageDownloaderTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DownloadAsync_ResumesPartialDownloadWhenServerReturnsRange()
    {
        Directory.CreateDirectory(_testDirectory);
        var destination = Path.Combine(_testDirectory, "package.zip");
        await File.WriteAllBytesAsync(destination + ".part", [1, 2]);

        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            Assert.Equal(2, request.Headers.Range?.Ranges.Single().From);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([3, 4, 5])
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 4, 5);
            return response;
        }));
        var downloader = new ResumableUpdatePackageDownloader(httpClient);

        await downloader.DownloadAsync(new Uri("https://updates.trynex.test/package.zip"), destination, 5);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, await File.ReadAllBytesAsync(destination));
        Assert.False(File.Exists(destination + ".part"));
    }

    [Fact]
    public async Task DownloadAsync_RestartsWhenServerIgnoresRange()
    {
        Directory.CreateDirectory(_testDirectory);
        var destination = Path.Combine(_testDirectory, "package.zip");
        await File.WriteAllBytesAsync(destination + ".part", [9, 9]);

        using var httpClient = new HttpClient(new DelegateHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3])
            }));
        var downloader = new ResumableUpdatePackageDownloader(httpClient);

        await downloader.DownloadAsync(new Uri("https://updates.trynex.test/package.zip"), destination, 3);

        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task DownloadAsync_RejectsPayloadLargerThanSignedSize()
    {
        Directory.CreateDirectory(_testDirectory);
        var destination = Path.Combine(_testDirectory, "package.zip");

        using var httpClient = new HttpClient(new DelegateHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            }));
        var downloader = new ResumableUpdatePackageDownloader(httpClient);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            downloader.DownloadAsync(new Uri("https://updates.trynex.test/package.zip"), destination, 3));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(callback(request));
        }
    }
}
