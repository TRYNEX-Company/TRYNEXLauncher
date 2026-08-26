using System.IO;
using System.Windows.Input;
using Trynex.Core.Abstractions;
using Trynex.Core.Settings;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class SettingsViewModel : PageViewModel
{
    private readonly ILauncherSettingsStore _settingsStore;
    private readonly ILocalizationService _localizationService;
    private string _selectedLanguage;
    private string _defaultInstallDirectory = string.Empty;
    private bool _startWithWindows;
    private bool _minimizeToTray = true;
    private bool _allowPrereleaseUpdates;
    private int _downloadLimitMbps;
    private string _statusMessage;
    private bool _isSaving;
    private SettingsStatus _status = SettingsStatus.LocalOnly;
    private DateTime? _savedAt;

    public SettingsViewModel(ILauncherSettingsStore settingsStore, ILocalizationService? localizationService = null)
        : base("settings", "Настройки")
    {
        _settingsStore = settingsStore;
        _localizationService = localizationService ?? new FallbackLocalizationService();
        _selectedLanguage = _localizationService.CurrentLanguage;
        _statusMessage = _localizationService.Get("Settings.Status.LocalOnly");
        Languages =
        [
            new("ru-RU", "Русский"),
            new("uk-UA", "Українська"),
            new("en-US", "English"),
            new("de-DE", "Deutsch")
        ];
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving);
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public ICommand SaveCommand { get; }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            var normalized = WpfLocalizationService.NormalizeLanguage(value);
            if (SetProperty(ref _selectedLanguage, normalized))
            {
                _localizationService.ApplyLanguage(normalized);
            }
        }
    }

    public string DefaultInstallDirectory
    {
        get => _defaultInstallDirectory;
        set => SetProperty(ref _defaultInstallDirectory, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public bool AllowPrereleaseUpdates
    {
        get => _allowPrereleaseUpdates;
        set => SetProperty(ref _allowPrereleaseUpdates, value);
    }

    public int DownloadLimitMbps
    {
        get => _downloadLimitMbps;
        set => SetProperty(ref _downloadLimitMbps, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value) && SaveCommand is AsyncRelayCommand command)
            {
                command.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        SelectedLanguage = settings.Language;
        DefaultInstallDirectory = settings.DefaultInstallDirectory;
        StartWithWindows = settings.StartWithWindows;
        MinimizeToTray = settings.MinimizeToTray;
        AllowPrereleaseUpdates = settings.AllowPrereleaseUpdates;
        DownloadLimitMbps = settings.DownloadLimitMbps;
    }

    private async Task SaveAsync()
    {
        IsSaving = true;
        SetStatus(SettingsStatus.Saving);

        try
        {
            var settings = new LauncherSettings
            {
                Language = SelectedLanguage,
                DefaultInstallDirectory = DefaultInstallDirectory,
                StartWithWindows = StartWithWindows,
                MinimizeToTray = MinimizeToTray,
                AllowPrereleaseUpdates = AllowPrereleaseUpdates,
                DownloadLimitMbps = DownloadLimitMbps
            };

            await _settingsStore.SaveAsync(settings);
            _savedAt = DateTime.Now;
            SetStatus(SettingsStatus.Saved);
        }
        catch (IOException)
        {
            SetStatus(SettingsStatus.IoError);
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus(SettingsStatus.AccessError);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        Title = _localizationService.Get("Nav.Settings");
        RefreshStatusMessage();
    }

    private void SetStatus(SettingsStatus status)
    {
        _status = status;
        RefreshStatusMessage();
    }

    private void RefreshStatusMessage()
    {
        StatusMessage = _status switch
        {
            SettingsStatus.Saving => _localizationService.Get("Settings.Status.Saving"),
            SettingsStatus.Saved => _localizationService.Format(
                "Settings.Status.Saved",
                (_savedAt ?? DateTime.Now).ToString("HH:mm")),
            SettingsStatus.IoError => _localizationService.Get("Settings.Status.IoError"),
            SettingsStatus.AccessError => _localizationService.Get("Settings.Status.AccessError"),
            _ => _localizationService.Get("Settings.Status.LocalOnly")
        };
    }

    private enum SettingsStatus
    {
        LocalOnly,
        Saving,
        Saved,
        IoError,
        AccessError
    }
}

public sealed record LanguageOption(string Code, string Name);
