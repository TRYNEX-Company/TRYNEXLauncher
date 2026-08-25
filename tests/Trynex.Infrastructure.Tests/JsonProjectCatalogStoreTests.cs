using System.Text;
using Trynex.Infrastructure.Projects;

namespace Trynex.Infrastructure.Tests;

public sealed class JsonProjectCatalogStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ReadsAndValidatesCatalog()
    {
        Directory.CreateDirectory(_testDirectory);
        var catalogPath = Path.Combine(_testDirectory, "projects.json");
        await File.WriteAllTextAsync(catalogPath, ValidCatalogJson, Encoding.UTF8);

        var catalog = await new JsonProjectCatalogStore(catalogPath).LoadAsync();

        var project = Assert.Single(catalog.Projects);
        Assert.Equal("mr-project", project.Id);
        Assert.Equal("MR PROJECT", project.Name.Resolve("en-US"));
    }

    [Fact]
    public async Task LoadAsync_RejectsCatalogWithUnsafeFilePath()
    {
        Directory.CreateDirectory(_testDirectory);
        var catalogPath = Path.Combine(_testDirectory, "projects.json");
        await File.WriteAllTextAsync(
            catalogPath,
            ValidCatalogJson.Replace("addons/package.bin", "../package.bin", StringComparison.Ordinal),
            Encoding.UTF8);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonProjectCatalogStore(catalogPath).LoadAsync());

        Assert.Contains("project.path.unsafe", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    private const string ValidCatalogJson = """
        {
          "schemaVersion": 1,
          "publishedAtUtc": "2026-08-11T00:00:00Z",
          "projects": [
            {
              "schemaVersion": 1,
              "id": "mr-project",
              "version": "1.0.0",
              "platform": "ArmaReforger",
              "name": { "values": { "en-US": "MR PROJECT" } },
              "description": { "values": { "en-US": "Description" } },
              "status": { "values": { "en-US": "READY" } },
              "statusColor": "#68D9FA",
              "artworkPath": "mr-project.png",
              "contentRoot": "projects/mr-project/1.0.0/",
              "launch": { "steamAppId": "1874880", "arguments": [] },
              "files": [
                {
                  "relativePath": "addons/package.bin",
                  "sourcePath": "package.bin",
                  "size": 3,
                  "sha256": "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"
                }
              ]
            }
          ]
        }
        """;
}
