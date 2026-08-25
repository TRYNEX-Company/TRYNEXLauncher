using System.Net;
using System.Text;
using Trynex.Infrastructure.News;

namespace Trynex.Infrastructure.Tests;

public sealed class CachedJsonNewsFeedStoreTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "trynex-news-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_UsesRemoteFeedAndWritesCache()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var cachePath = Path.Combine(_temporaryDirectory, "cache", "news.json");
        var bundledPath = WriteFeed("bundled.json", "bundled");
        using var client = CreateClient(HttpStatusCode.OK, FeedJson("remote"));
        var store = CreateStore(client, cachePath, bundledPath);

        var feed = await store.LoadAsync();

        Assert.Equal("remote", Assert.Single(feed.Articles).Id);
        Assert.True(File.Exists(cachePath));
    }

    [Fact]
    public async Task LoadAsync_UsesCacheWhenRemoteIsUnavailable()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var cachePath = WriteFeed("cache.json", "cached");
        var bundledPath = WriteFeed("bundled.json", "bundled");
        using var client = CreateClient(HttpStatusCode.ServiceUnavailable, "offline");
        var store = CreateStore(client, cachePath, bundledPath);

        var feed = await store.LoadAsync();

        Assert.Equal("cached", Assert.Single(feed.Articles).Id);
    }

    [Fact]
    public async Task LoadAsync_UsesBundledFeedWhenRemoteAndCacheAreInvalid()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var cachePath = Path.Combine(_temporaryDirectory, "cache.json");
        File.WriteAllText(cachePath, "not-json");
        var bundledPath = WriteFeed("bundled.json", "bundled");
        using var client = CreateClient(HttpStatusCode.NotFound, "missing");
        var store = CreateStore(client, cachePath, bundledPath);

        var feed = await store.LoadAsync();

        Assert.Equal("bundled", Assert.Single(feed.Articles).Id);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, true);
        }
    }

    private CachedJsonNewsFeedStore CreateStore(HttpClient client, string cachePath, string bundledPath) => new(
        client,
        new Uri("https://trynex.dev/data/launcher-news.json"),
        cachePath,
        bundledPath);

    private string WriteFeed(string name, string id)
    {
        var path = Path.Combine(_temporaryDirectory, name);
        File.WriteAllText(path, FeedJson(id));
        return path;
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string content) => new(
        new StubHandler(statusCode, content));

    private static string FeedJson(string id) => $$"""
        {
          "schemaVersion": 1,
          "publishedAtUtc": "2026-08-11T10:00:00Z",
          "articles": [
            {
              "id": "{{id}}",
              "category": "Launcher",
              "projectId": null,
              "publishedAtUtc": "2026-08-11T10:00:00Z",
              "isFeatured": true,
              "title": { "values": { "en-US": "Title" } },
              "summary": { "values": { "en-US": "Summary" } },
              "artworkPath": "/Trynex.Launcher;component/Assets/Brand/trynex-mark.png",
              "link": "https://trynex.dev"
            }
          ]
        }
        """;

    private sealed class StubHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        });
    }
}
