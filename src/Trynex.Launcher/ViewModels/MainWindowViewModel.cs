using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows.Input;
using Trynex.Core.Abstractions;
using Trynex.Core.Identity;
using Trynex.Core.News;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly Dictionary<string, PageViewModel> _pages;
    private readonly IInAppUpdateService? _updateService;
    private readonly ILocalizationService _localizationService;
    private readonly LibraryViewModel _library;
    private readonly HomeViewModel _home;
    private readonly CommunityViewModel _community;
    private readonly INewsFeedStore? _newsFeedStore;
    private readonly ITrynexIdentityService? _identityService;
    private readonly string _currentVersion;
    private readonly RelayCommand _installUpdateCommand;
    private readonly HashSet<string> _dismissedAnnouncementIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownNotificationIds = new(StringComparer.OrdinalIgnoreCase);
    private PageViewModel _currentPage;
    private string _globalSearchText = string.Empty;
    private bool _isUpdateAvailable;
    private string? _availableUpdateVersion;
    private string _updateButtonTitle = string.Empty;
    private NewsFeed? _newsFeed;
    private bool _isNotificationCenterOpen;
    private int _unreadNotificationCount;
    private NotificationItemViewModel? _activeAnnouncement;
    private TrynexIdentityProfile? _identityProfile;
    private string _identityDisplayName = "TRYNEX ID";
    private string _identityStatus = string.Empty;
    private string _identityInitials = "TX";
    private string _identityActionTitle = string.Empty;
    private string _identityActionGlyph = "\uE77B";
    private readonly AsyncRelayCommand _identityActionCommand;

    public MainWindowViewModel(
        ILauncherSettingsStore settingsStore,
        IInAppUpdateService? updateService = null,
        string? currentVersion = null,
        ILocalizationService? localizationService = null,
        IProjectCatalogStore? projectCatalogStore = null,
        INewsFeedStore? newsFeedStore = null,
        ITrynexIdentityService? identityService = null)
    {
        _updateService = updateService;
        _localizationService = localizationService ?? new FallbackLocalizationService();
        _newsFeedStore = newsFeedStore;
        _identityService = identityService;
        _currentVersion = currentVersion ?? GetCurrentVersion();

        NavigateCommand = new RelayCommand(
            parameter => Navigate(parameter as string ?? "home"),
            parameter => parameter is string key && !string.IsNullOrWhiteSpace(key));
        _installUpdateCommand = new RelayCommand(
            InstallUpdate,
            () => IsUpdateAvailable && _updateService is not null);
        InstallUpdateCommand = _installUpdateCommand;
        DismissAnnouncementCommand = new RelayCommand(
            parameter => DismissAnnouncement(parameter as string),
            parameter => parameter is string id && !string.IsNullOrWhiteSpace(id));
        _identityActionCommand = new AsyncRelayCommand(
            ToggleIdentityAsync,
            () => _identityService is not null);
        IdentityActionCommand = _identityActionCommand;
        Notifications = [];

        Settings = new SettingsViewModel(settingsStore, _localizationService);
        _library = new LibraryViewModel(_localizationService, projectCatalogStore);
        _library.ProjectOpenRequested += Library_ProjectOpenRequested;
        _home = new HomeViewModel(
            Navigate,
            _localizationService.Get("Nav.Home"),
            _localizationService);
        _community = new CommunityViewModel(
            _localizationService.Get("Nav.Community"),
            _localizationService);
        _pages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = _home,
            ["library"] = _library,
            ["downloads"] = new DownloadsViewModel(_localizationService.Get("Nav.Downloads")),
            ["community"] = _community,
            ["messenger"] = new MessengerViewModel(_localizationService.Get("Nav.Messenger")),
            ["settings"] = Settings
        };

        PrimaryNavigation =
        [
            new("home", _localizationService.Get("Nav.Home"), "\uE80F", NavigateCommand),
            new("library", _localizationService.Get("Nav.Library"), "\uE8F1", NavigateCommand),
            new("downloads", _localizationService.Get("Nav.Downloads"), "\uE896", NavigateCommand, "0"),
            new("community", _localizationService.Get("Nav.Community"), "\uE716", NavigateCommand)
        ];

        ServiceNavigation =
        [
            new("messenger", _localizationService.Get("Nav.Messenger"), "\uE8BD", NavigateCommand, _localizationService.Get("Common.Soon"))
        ];

        SettingsNavigation = new("settings", _localizationService.Get("Nav.Settings"), "\uE713", NavigateCommand);
        _currentPage = _pages["home"];
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
        ApplyLocalization();
        UpdateSelection("home");
    }

    public ObservableCollection<NavigationItemViewModel> PrimaryNavigation { get; }

    public ObservableCollection<NavigationItemViewModel> ServiceNavigation { get; }

    public NavigationItemViewModel SettingsNavigation { get; }

    public SettingsViewModel Settings { get; }

    public ICommand NavigateCommand { get; }

    public ICommand InstallUpdateCommand { get; }

    public ICommand DismissAnnouncementCommand { get; }

    public ICommand IdentityActionCommand { get; }

    public ObservableCollection<NotificationItemViewModel> Notifications { get; }

    public event EventHandler? UpdateRestartRequested;

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string GlobalSearchText
    {
        get => _globalSearchText;
        set => SetProperty(ref _globalSearchText, value);
    }

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        private set
        {
            if (SetProperty(ref _isUpdateAvailable, value))
            {
                _installUpdateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? AvailableUpdateVersion
    {
        get => _availableUpdateVersion;
        private set => SetProperty(ref _availableUpdateVersion, value);
    }

    public string UpdateButtonTitle
    {
        get => _updateButtonTitle;
        private set => SetProperty(ref _updateButtonTitle, value);
    }

    public bool IsNotificationCenterOpen
    {
        get => _isNotificationCenterOpen;
        set
        {
            if (SetProperty(ref _isNotificationCenterOpen, value) && value)
            {
                UnreadNotificationCount = 0;
            }
        }
    }

    public int UnreadNotificationCount
    {
        get => _unreadNotificationCount;
        private set
        {
            if (SetProperty(ref _unreadNotificationCount, value))
            {
                OnPropertyChanged(nameof(HasUnreadNotifications));
                OnPropertyChanged(nameof(NotificationBadge));
            }
        }
    }

    public bool HasUnreadNotifications => UnreadNotificationCount > 0;

    public string NotificationBadge => UnreadNotificationCount > 9
        ? "9+"
        : UnreadNotificationCount.ToString(CultureInfo.InvariantCulture);

    public bool HasNotifications => Notifications.Count > 0;

    public bool HasNoNotifications => !HasNotifications;

    public NotificationItemViewModel? ActiveAnnouncement
    {
        get => _activeAnnouncement;
        private set
        {
            if (SetProperty(ref _activeAnnouncement, value))
            {
                OnPropertyChanged(nameof(HasActiveAnnouncement));
            }
        }
    }

    public bool HasActiveAnnouncement => ActiveAnnouncement is not null;

    public string IdentityDisplayName
    {
        get => _identityDisplayName;
        private set => SetProperty(ref _identityDisplayName, value);
    }

    public string IdentityStatus
    {
        get => _identityStatus;
        private set => SetProperty(ref _identityStatus, value);
    }

    public string IdentityInitials
    {
        get => _identityInitials;
        private set => SetProperty(ref _identityInitials, value);
    }

    public string IdentityActionTitle
    {
        get => _identityActionTitle;
        private set => SetProperty(ref _identityActionTitle, value);
    }

    public string IdentityActionGlyph
    {
        get => _identityActionGlyph;
        private set => SetProperty(ref _identityActionGlyph, value);
    }

    public bool IsIdentitySignedIn => _identityProfile is not null;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Settings.LoadAsync(cancellationToken);
        await Task.WhenAll(
            _library.LoadAsync(cancellationToken),
            LoadNewsAsync(cancellationToken),
            RestoreIdentityAsync(cancellationToken));
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (_updateService is null)
        {
            return;
        }

        try
        {
            var result = await _updateService.CheckAsync(_currentVersion, cancellationToken);
            AvailableUpdateVersion = result.AvailableVersion;
            IsUpdateAvailable = !string.IsNullOrWhiteSpace(result.AvailableVersion);
            RefreshUpdateButtonTitle();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // A temporary R2 or network failure must not interrupt the running launcher.
            // Keep an already discovered update visible and retry on the next polling cycle.
        }
    }

    public async Task MonitorUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckForUpdatesAsync(cancellationToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await CheckForUpdatesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal application shutdown.
        }
    }

    public async Task MonitorNewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await LoadNewsAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during normal application shutdown.
        }
    }

    public void Navigate(string key)
    {
        const string projectPrefix = "project:";
        if (key.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            OpenProject(key[projectPrefix.Length..]);
            return;
        }

        if (!_pages.TryGetValue(key, out var page))
        {
            return;
        }

        CurrentPage = page;
        UpdateSelection(key);
    }

    private async Task LoadNewsAsync(CancellationToken cancellationToken)
    {
        if (_newsFeedStore is null)
        {
            return;
        }

        try
        {
            _newsFeed = await _newsFeedStore.LoadAsync(cancellationToken);
            _home.SetNews(_newsFeed);
            _community.SetNews(_newsFeed);
            RebuildNotifications(_newsFeed);
            if (CurrentPage is ProjectDetailViewModel project)
            {
                project.SetNews(_newsFeed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException)
        {
            // The launcher remains usable even if every news source is unavailable.
        }
    }

    private void Library_ProjectOpenRequested(object? sender, GameCardViewModel project) => OpenProject(project.Id);

    private void OpenProject(string projectId)
    {
        var project = _library.FindById(projectId);
        if (project is null)
        {
            return;
        }

        var detail = new ProjectDetailViewModel(project, Navigate, _localizationService);
        detail.SetNews(_newsFeed);
        CurrentPage = detail;
        UpdateSelection("library");
    }

    private void UpdateSelection(string selectedKey)
    {
        foreach (var item in PrimaryNavigation.Concat(ServiceNavigation).Append(SettingsNavigation))
        {
            item.IsSelected = string.Equals(item.Key, selectedKey, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void InstallUpdate()
    {
        if (_updateService?.TryLaunchBootstrapper() == true)
        {
            UpdateRestartRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        UpdateButtonTitle = _localizationService.Get("Update.Button.Failed");
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e) => ApplyLocalization();

    private void ApplyLocalization()
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = _localizationService.Get("Nav.Home"),
            ["library"] = _localizationService.Get("Nav.Library"),
            ["downloads"] = _localizationService.Get("Nav.Downloads"),
            ["community"] = _localizationService.Get("Nav.Community"),
            ["messenger"] = _localizationService.Get("Nav.Messenger"),
            ["settings"] = _localizationService.Get("Nav.Settings")
        };

        foreach (var item in PrimaryNavigation.Concat(ServiceNavigation).Append(SettingsNavigation))
        {
            item.Title = titles[item.Key];
        }

        ServiceNavigation[0].Badge = _localizationService.Get("Common.Soon");
        foreach (var page in _pages.Values)
        {
            page.Title = titles[page.Key];
        }

        foreach (var notification in Notifications)
        {
            notification.ApplyLanguage();
        }

        RefreshUpdateButtonTitle();
        RefreshIdentityText();
    }

    private async Task RestoreIdentityAsync(CancellationToken cancellationToken)
    {
        if (_identityService is null)
        {
            SetIdentityProfile(null);
            return;
        }

        try
        {
            SetIdentityProfile(await _identityService.RestoreAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            SetIdentityProfile(null, "Identity.Status.Unavailable");
        }
    }

    private async Task ToggleIdentityAsync()
    {
        if (_identityService is null)
        {
            return;
        }

        try
        {
            if (_identityProfile is null)
            {
                IdentityStatus = _localizationService.Get("Identity.Status.OpeningBrowser");
                SetIdentityProfile(await _identityService.SignInAsync());
            }
            else
            {
                await _identityService.SignOutAsync();
                SetIdentityProfile(null);
            }
        }
        catch (OperationCanceledException)
        {
            SetIdentityProfile(null, "Identity.Status.Cancelled");
        }
        catch (Exception)
        {
            SetIdentityProfile(null, "Identity.Status.Failed");
        }
    }

    private void SetIdentityProfile(TrynexIdentityProfile? profile, string? statusKey = null)
    {
        _identityProfile = profile;
        IdentityDisplayName = profile?.DisplayName ?? "TRYNEX ID";
        IdentityInitials = CreateInitials(profile?.DisplayName);
        IdentityStatus = profile?.Email ?? _localizationService.Get(statusKey ?? "Identity.Status.SignedOut");
        OnPropertyChanged(nameof(IsIdentitySignedIn));
        RefreshIdentityText();
        _identityActionCommand.NotifyCanExecuteChanged();
    }

    private void RefreshIdentityText()
    {
        if (_identityProfile is null && string.IsNullOrWhiteSpace(IdentityStatus))
        {
            IdentityStatus = _localizationService.Get("Identity.Status.SignedOut");
        }
        IdentityActionTitle = _localizationService.Get(
            _identityProfile is null ? "Identity.Action.SignIn" : "Identity.Action.SignOut");
        IdentityActionGlyph = _identityProfile is null ? "\uE77B" : "\uE8AC";
    }

    private static string CreateInitials(string? displayName)
    {
        var parts = (displayName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "TX";
        }

        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    private void RebuildNotifications(NewsFeed feed)
    {
        var now = DateTimeOffset.UtcNow;
        var announcements = (feed.Announcements ?? [])
            .Where(item => item.StartsAtUtc <= now && item.EndsAtUtc > now)
            .Where(item => !_dismissedAnnouncementIds.Contains(item.Id))
            .OrderByDescending(item => SeverityRank(item.Severity))
            .ThenByDescending(item => item.StartsAtUtc)
            .Select(item => new NotificationItemViewModel(item, _localizationService))
            .ToList();

        var featuredNews = feed.Articles
            .Where(item => item.IsFeatured)
            .OrderByDescending(item => item.PublishedAtUtc)
            .Take(5)
            .Select(item => new NotificationItemViewModel(item, _localizationService));

        var items = announcements.Concat(featuredNews).ToList();
        var newUnread = items.Count(item => !_knownNotificationIds.Contains(item.Id));

        Notifications.Clear();
        foreach (var item in items)
        {
            Notifications.Add(item);
            _knownNotificationIds.Add(item.Id);
        }

        if (!IsNotificationCenterOpen && newUnread > 0)
        {
            UnreadNotificationCount = Math.Min(99, UnreadNotificationCount + newUnread);
        }

        ActiveAnnouncement = announcements.FirstOrDefault();
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(HasNoNotifications));
    }

    private void DismissAnnouncement(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || _newsFeed is null)
        {
            return;
        }

        _dismissedAnnouncementIds.Add(id);
        RebuildNotifications(_newsFeed);
    }

    private static int SeverityRank(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 4,
        "warning" => 3,
        "maintenance" => 2,
        _ => 1
    };

    private void RefreshUpdateButtonTitle()
    {
        UpdateButtonTitle = IsUpdateAvailable && !string.IsNullOrWhiteSpace(AvailableUpdateVersion)
            ? _localizationService.Format("Update.Button.Version", AvailableUpdateVersion)
            : _localizationService.Get("Update.Button.Default");
    }

    private static string GetCurrentVersion()
    {
        var informationalVersion = typeof(MainWindowViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? "0.0.0-local"
            : informationalVersion.Split('+', 2)[0];
    }
}
