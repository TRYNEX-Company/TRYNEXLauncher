using Trynex.Core.Settings;
using Trynex.Infrastructure.Settings;

namespace Trynex.Infrastructure.Tests;

public sealed class JsonLauncherSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsNormalizedSettings()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var store = new JsonLauncherSettingsStore(settingsPath);
            var settings = new LauncherSettings
            {
                Language = "en-US",
                DefaultInstallDirectory = "  D:\\Games  ",
                DownloadLimitMbps = 250,
                MinimizeToTray = false
            };

            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            Assert.Equal("en-US", loaded.Language);
            Assert.Equal("D:\\Games", loaded.DefaultInstallDirectory);
            Assert.Equal(250, loaded.DownloadLimitMbps);
            Assert.False(loaded.MinimizeToTray);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_ForMalformedJson()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            await File.WriteAllTextAsync(settingsPath, "{ definitely not json }");
            var store = new JsonLauncherSettingsStore(settingsPath);

            var loaded = await store.LoadAsync();

            Assert.Equal(new LauncherSettings(), loaded);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "trynex-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
