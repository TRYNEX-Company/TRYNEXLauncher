using System.Reflection;
using Trynex.Core.Abstractions;
using Trynex.Core.News;
using Trynex.Core.Projects;
using Trynex.Core.Settings;
using Trynex.Launcher.Services;
using Trynex.Launcher.ViewModels;

namespace Trynex.Launcher.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_SelectsHomePage()
    {
        var viewModel = new MainWindowViewModel(new InMemorySettingsStore());

        Assert.Equal("home", viewModel.CurrentPage.Key);
        Assert.True(viewModel.PrimaryNavigation.Single(item => item.Key == "home").IsSelected);
    }

    [Fact]
    public void Navigate_ChangesPageAndSelectedNavigationItem()
    {
        var viewModel = new MainWindowViewModel(new InMemorySettingsStore());

        viewModel.Navigate("downloads");

        Assert.Equal("downloads", viewModel.CurrentPage.Key);
        Assert.True(viewModel.PrimaryNavigation.Single(item => item.Key == "downloads").IsSelected);
        Assert.False(viewModel.PrimaryNavigation.Single(item => item.Key == "home").IsSelected);
    }

    [Fact]
    public async Task InitializeAsync_LoadsPersistedSettings()
    {
        var persisted = new LauncherSettings
        {
            Language = "uk-UA",
            DownloadLimitMbps = 120,
            MinimizeToTray = false
        };
        var viewModel = new MainWindowViewModel(new InMemorySettingsStore(persisted));

        await viewModel.InitializeAsync();

        Assert.Equal("uk-UA", viewModel.Settings.SelectedLanguage);
        Assert.Equal(120, viewModel.Settings.DownloadLimitMbps);
        Assert.False(viewModel.Settings.MinimizeToTray);
    }

    [Fact]
    public void Navigate_ProjectId_OpensProjectDetailAndKeepsLibrarySelected()
    {
        var viewModel = new MainWindowViewModel(new InMemorySettingsStore());

        viewModel.Navigate("project:mr-project");

        var project = Assert.IsType<ProjectDetailViewModel>(viewModel.CurrentPage);
        Assert.Equal("mr-project", project.Id);
        Assert.True(viewModel.PrimaryNavigation.Single(item => item.Key == "library").IsSelected);
    }

    [Fact]
    public async Task InitializeAsync_LoadsNewsForHomeAndCommunity()
    {
        var viewModel = new MainWindowViewModel(
            new InMemorySettingsStore(),
            newsFeedStore: new FakeNewsFeedStore());

        await viewModel.InitializeAsync();
        viewModel.Navigate("community");

        var community = Assert.IsType<CommunityViewModel>(viewModel.CurrentPage);
        Assert.Single(community.News);
        Assert.Equal("News title", community.News[0].Title);
    }

    [Fact]
    public async Task InitializeAsync_BuildsNotificationCenterAndDismissesActiveAnnouncement()
    {
        var now = DateTimeOffset.UtcNow;
        var feed = new NewsFeed(
            1,
            now,
            [
                new NewsArticle(
                    "featured",
                    "Launcher",
                    null,
                    now,
                    true,
                    Text("Featured title"),
                    Text("Featured summary"),
                    "/Trynex.Launcher;component/Assets/Brand/trynex-mark.png",
                    "https://trynex.dev")
            ],
            [
                new SystemAnnouncement(
                    "maintenance",
                    "maintenance",
                    now.AddMinutes(-5),
                    now.AddHours(2),
                    true,
                    Text("Maintenance"),
                    Text("Short interruption"),
                    null)
            ]);
        var viewModel = new MainWindowViewModel(
            new InMemorySettingsStore(),
            newsFeedStore: new FakeNewsFeedStore(feed));

        await viewModel.InitializeAsync();

        Assert.Equal(2, viewModel.Notifications.Count);
        Assert.Equal("maintenance", viewModel.ActiveAnnouncement?.Id);
        Assert.Equal(2, viewModel.UnreadNotificationCount);

        viewModel.IsNotificationCenterOpen = true;
        Assert.Equal(0, viewModel.UnreadNotificationCount);

        viewModel.DismissAnnouncementCommand.Execute("maintenance");
        Assert.Null(viewModel.ActiveAnnouncement);
        Assert.Single(viewModel.Notifications);
    }

    [Fact]
    public void HomeVersionDisplay_ComesFromAssemblyInformationalVersion()
    {
        var home = new HomeViewModel(_ => { });
        var informationalVersion = typeof(HomeViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .Split('+', 2)[0];

        Assert.Equal(
            informationalVersion.Replace("-", " · ", StringComparison.Ordinal).ToUpperInvariant(),
            home.VersionDisplay);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShowsTrustedNewerVersion()
    {
        var updateService = new FakeUpdateService("0.3.0-preview.6");
        var viewModel = new MainWindowViewModel(
            new InMemorySettingsStore(),
            updateService,
            "0.3.0-preview.5");

        await viewModel.CheckForUpdatesAsync();

        Assert.True(viewModel.IsUpdateAvailable);
        Assert.Equal("0.3.0-preview.6", viewModel.AvailableUpdateVersion);
        Assert.Equal("ОБНОВИТЬ ДО 0.3.0-preview.6", viewModel.UpdateButtonTitle);
        Assert.Equal("0.3.0-preview.5", updateService.CheckedCurrentVersion);
        Assert.True(viewModel.InstallUpdateCommand.CanExecute(null));
    }

    [Fact]
    public async Task InstallUpdateCommand_StartsBootstrapperAndRequestsLauncherShutdown()
    {
        var updateService = new FakeUpdateService("0.3.0-preview.6", launchSucceeds: true);
        var viewModel = new MainWindowViewModel(
            new InMemorySettingsStore(),
            updateService,
            "0.3.0-preview.5");
        var restartRequested = false;
        viewModel.UpdateRestartRequested += (_, _) => restartRequested = true;
        await viewModel.CheckForUpdatesAsync();

        viewModel.InstallUpdateCommand.Execute(null);

        Assert.True(updateService.LaunchAttempted);
        Assert.True(restartRequested);
    }

    [Fact]
    public async Task ChangingLanguage_ImmediatelyRelocalizesNavigationAndUpdateButton()
    {
        var localization = new FakeLocalizationService();
        var updateService = new FakeUpdateService("0.3.0-preview.8");
        var viewModel = new MainWindowViewModel(
            new InMemorySettingsStore(),
            updateService,
            "0.3.0-preview.7",
            localization);
        await viewModel.CheckForUpdatesAsync();

        viewModel.Settings.SelectedLanguage = "en-US";

        Assert.Equal("Home", viewModel.PrimaryNavigation[0].Title);
        Assert.Equal("UPDATE TO 0.3.0-preview.8", viewModel.UpdateButtonTitle);
        Assert.Equal("SOON", viewModel.ServiceNavigation[0].Badge);
    }

    private sealed class InMemorySettingsStore(LauncherSettings? settings = null) : ILauncherSettingsStore
    {
        public Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(settings ?? new LauncherSettings());

        public Task SaveAsync(LauncherSettings settingsToSave, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUpdateService(
        string? availableVersion,
        bool launchSucceeds = false) : IInAppUpdateService
    {
        public string? CheckedCurrentVersion { get; private set; }

        public bool LaunchAttempted { get; private set; }

        public Task<InAppUpdateCheckResult> CheckAsync(
            string currentVersion,
            CancellationToken cancellationToken = default)
        {
            CheckedCurrentVersion = currentVersion;
            return Task.FromResult(new InAppUpdateCheckResult(availableVersion));
        }

        public bool TryLaunchBootstrapper()
        {
            LaunchAttempted = true;
            return launchSucceeds;
        }
    }

    private sealed class FakeNewsFeedStore(NewsFeed? feed = null) : INewsFeedStore
    {
        public Task<NewsFeed> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(
            feed ?? new NewsFeed(
                1,
                DateTimeOffset.UtcNow,
                [
                    new NewsArticle(
                        "news",
                        "Launcher",
                        null,
                        DateTimeOffset.UtcNow,
                        true,
                        Text("News title"),
                        Text("News summary"),
                        "/Trynex.Launcher;component/Assets/Brand/trynex-mark.png",
                        "https://trynex.dev")
                ]));

        private static LocalizedProjectText Text(string value) => new(
            new Dictionary<string, string>
            {
                ["ru-RU"] = value,
                ["en-US"] = value
            });
    }

    private static LocalizedProjectText Text(string value) => new(
        new Dictionary<string, string>
        {
            ["ru-RU"] = value,
            ["en-US"] = value
        });

    private sealed class FakeLocalizationService : ILocalizationService
    {
        private static readonly IReadOnlyDictionary<string, string> Russian =
            new Dictionary<string, string>
            {
                ["Nav.Home"] = "Главная",
                ["Nav.Library"] = "Библиотека",
                ["Nav.Downloads"] = "Загрузки",
                ["Nav.Community"] = "Сообщество",
                ["Nav.Messenger"] = "Мессенджер",
                ["Nav.Settings"] = "Настройки",
                ["Common.Soon"] = "СКОРО",
                ["Update.Button.Default"] = "ОБНОВИТЬ TRYNEX",
                ["Update.Button.Version"] = "ОБНОВИТЬ ДО {0}",
                ["Update.Button.Failed"] = "ОШИБКА",
                ["Settings.Status.LocalOnly"] = "Локально",
                ["Settings.Status.Saving"] = "Сохраняем",
                ["Settings.Status.Saved"] = "Сохранено {0}",
                ["Settings.Status.IoError"] = "Ошибка ввода-вывода",
                ["Settings.Status.AccessError"] = "Нет доступа",
                ["Library.Mr.Description"] = "Описание",
                ["Library.Mr.Status"] = "ПЕРЕХОД"
            };

        private static readonly IReadOnlyDictionary<string, string> English =
            new Dictionary<string, string>
            {
                ["Nav.Home"] = "Home",
                ["Nav.Library"] = "Library",
                ["Nav.Downloads"] = "Downloads",
                ["Nav.Community"] = "Community",
                ["Nav.Messenger"] = "Messenger",
                ["Nav.Settings"] = "Settings",
                ["Common.Soon"] = "SOON",
                ["Update.Button.Default"] = "UPDATE TRYNEX",
                ["Update.Button.Version"] = "UPDATE TO {0}",
                ["Update.Button.Failed"] = "ERROR",
                ["Settings.Status.LocalOnly"] = "Local",
                ["Settings.Status.Saving"] = "Saving",
                ["Settings.Status.Saved"] = "Saved {0}",
                ["Settings.Status.IoError"] = "I/O error",
                ["Settings.Status.AccessError"] = "Access denied",
                ["Library.Mr.Description"] = "Description",
                ["Library.Mr.Status"] = "MOVING"
            };

        public string CurrentLanguage { get; private set; } = "ru-RU";

        public event EventHandler? LanguageChanged;

        public void ApplyLanguage(string languageCode)
        {
            CurrentLanguage = languageCode;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Get(string key)
        {
            var dictionary = CurrentLanguage == "en-US" ? English : Russian;
            return dictionary.TryGetValue(key, out var value) ? value : key;
        }

        public string Format(string key, params object?[] arguments) => string.Format(Get(key), arguments);
    }
}
