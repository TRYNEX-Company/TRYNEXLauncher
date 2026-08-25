using System.Net;
using System.Text;
using Trynex.Infrastructure.Networking;

namespace Trynex.Infrastructure.Tests;

public sealed class JsonUpdateManifestClientTests
{
    [Fact]
    public async Task GetAsync_ParsesManifestFromHttpsEndpoint()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "product": "TRYNEX.Launcher",
              "channel": "preview",
              "version": "0.3.0-preview.1",
              "publishedAtUtc": "2026-08-08T06:00:00Z",
              "packagePath": "launcher/preview/0.3.0-preview.1/trynex.zip",
              "packageSize": 5,
              "packageSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "signature": "AQID"
            }
            """;
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));
        var client = new JsonUpdateManifestClient(httpClient);

        var manifest = await client.GetAsync(new Uri("https://updates.trynex.test/preview/manifest.json"));

        Assert.Equal("0.3.0-preview.1", manifest.Version);
        Assert.Equal(5, manifest.PackageSize);
    }

    [Fact]
    public async Task GetAsync_RejectsNonHttpsEndpoint()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)));
        var client = new JsonUpdateManifestClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetAsync(new Uri("http://updates.trynex.test/manifest.json")));
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
