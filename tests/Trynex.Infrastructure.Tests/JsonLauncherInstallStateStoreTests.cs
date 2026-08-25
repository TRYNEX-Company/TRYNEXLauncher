using Trynex.Core.Updates;
using Trynex.Infrastructure.Updates;

namespace Trynex.Infrastructure.Tests;

public sealed class JsonLauncherInstallStateStoreTests : IDisposable
{
    private readonly string _testDirectory = Path.Combine(
        Path.GetTempPath(),
        "TRYNEX.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsStateAtomically()
    {
        var path = Path.Combine(_testDirectory, "state.json");
        var store = new JsonLauncherInstallStateStore(path);
        var expected = new LauncherInstallState(
            "0.3.0-preview.1",
            "0.2.0-preview.1",
            "0.3.0-preview.1",
            "0.1.0-preview.9");

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_DropsInvalidVersionValues()
    {
        Directory.CreateDirectory(_testDirectory);
        var path = Path.Combine(_testDirectory, "state.json");
        await File.WriteAllTextAsync(
            path,
            "{\"activeVersion\":\"../../escape\",\"failedVersion\":\"not-a-version\"}");
        var store = new JsonLauncherInstallStateStore(path);

        var state = await store.LoadAsync();

        Assert.Null(state.ActiveVersion);
        Assert.Null(state.FailedVersion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
