using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using Trynex.Infrastructure.News;
using Trynex.Infrastructure.Projects;
using Trynex.Infrastructure.Identity;
using Trynex.Infrastructure.Settings;
using Trynex.Infrastructure.Updates;
using Trynex.Launcher.Services;
using Trynex.Launcher.ViewModels;

namespace Trynex.Launcher;

public partial class App : Application
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private InAppUpdateService? _updateService;
    private HttpClient? _newsHttpClient;
    private HttpClient? _identityHttpClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsStore = new JsonLauncherSettingsStore();
        var localizationService = new WpfLocalizationService();
        var initialSettings = await settingsStore.LoadAsync();
        localizationService.ApplyLanguage(initialSettings.Language);
        var projectCatalogStore = new JsonProjectCatalogStore(Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Catalog",
            "projects.json"));
        _newsHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
        _newsHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TRYNEX-Launcher/0.3");
        _identityHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        _identityHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("TRYNEX-Launcher/0.3");
        var identityService = new TrynexIdentityClient(
            _identityHttpClient,
            new DpapiIdentityTokenStore(),
            _ => Task.FromResult<IIdentityAuthorizationReceiver>(SystemBrowserIdentityReceiver.Create()));
        var newsFeedStore = new CachedJsonNewsFeedStore(
            _newsHttpClient,
            new Uri("https://trynex.dev/data/launcher-news.json"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TRYNEX",
                "Launcher",
                "cache",
                "news.json"),
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Content",
                "news.json"));
        _ = InAppUpdateService.TryCreateDefault(out _updateService);
        var viewModel = new MainWindowViewModel(
            settingsStore,
            _updateService,
            localizationService: localizationService,
            projectCatalogStore: projectCatalogStore,
            newsFeedStore: newsFeedStore,
            identityService: identityService);
        viewModel.UpdateRestartRequested += ViewModel_UpdateRestartRequested;
        var window = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = window;
        window.Show();

        await viewModel.InitializeAsync();
        await TryWriteHealthMarkerAsync(e.Args);
        await TryCleanupOldLauncherVersionsAsync();
        _ = viewModel.MonitorUpdatesAsync(_lifetimeCancellation.Token);
        _ = viewModel.MonitorNewsAsync(_lifetimeCancellation.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _updateService?.Dispose();
        _newsHttpClient?.Dispose();
        _identityHttpClient?.Dispose();
        _lifetimeCancellation.Dispose();
        base.OnExit(e);
    }

    private void ViewModel_UpdateRestartRequested(object? sender, EventArgs e)
    {
        Shutdown();
    }

    private static async Task TryWriteHealthMarkerAsync(string[] arguments)
    {
        const string markerArgument = "--trynex-health-marker";
        var markerIndex = Array.FindIndex(
            arguments,
            argument => string.Equals(argument, markerArgument, StringComparison.Ordinal));

        if (markerIndex < 0 || markerIndex + 1 >= arguments.Length)
        {
            return;
        }

        try
        {
            var healthRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TRYNEX",
                "Launcher",
                "health"));
            var markerPath = Path.GetFullPath(arguments[markerIndex + 1]);
            var healthPrefix = Path.TrimEndingDirectorySeparator(healthRoot) + Path.DirectorySeparatorChar;

            if (!markerPath.StartsWith(healthPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Directory.CreateDirectory(healthRoot);
            var temporaryPath = markerPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, DateTimeOffset.UtcNow.ToString("O"));
            File.Move(temporaryPath, markerPath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Failure to write health status must not crash an otherwise working launcher.
        }
    }

    private static async Task TryCleanupOldLauncherVersionsAsync()
    {
        try
        {
            var launcherRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TRYNEX",
                "Launcher"));
            var stateStore = new JsonLauncherInstallStateStore(
                Path.Combine(launcherRoot, "state.json"));
            var currentVersion = GetCurrentVersion();

            // During an update the bootstrapper clears PendingVersion only after the
            // health marker is accepted. Never prune rollback folders before that.
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var state = await stateStore.LoadAsync();
                if (!string.Equals(
                        state.ActiveVersion,
                        currentVersion,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (state.PendingVersion is null)
                {
                    var retention = new LauncherVersionRetentionService();
                    retention.Cleanup(Path.Combine(launcherRoot, "versions"), state);
                    return;
                }

                await Task.Delay(250);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Cleanup is best effort and must never prevent the launcher from starting.
        }
    }

    private static string GetCurrentVersion()
    {
        var informationalVersion = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? "0.0.0-local"
            : informationalVersion.Split('+', 2)[0];
    }
}
