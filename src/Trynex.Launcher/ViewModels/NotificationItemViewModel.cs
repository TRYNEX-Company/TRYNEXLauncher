using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Trynex.Core.News;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class NotificationItemViewModel : ObservableObject
{
    private readonly SystemAnnouncement? _announcement;
    private readonly NewsArticle? _article;
    private readonly ILocalizationService _localizationService;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private string _kind = string.Empty;
    private string _dateDisplay = string.Empty;

    public NotificationItemViewModel(
        SystemAnnouncement announcement,
        ILocalizationService localizationService)
    {
        _announcement = announcement;
        _localizationService = localizationService;
        OpenLinkCommand = new RelayCommand(OpenLink, () => HasLink);
        ApplyLanguage();
    }

    public NotificationItemViewModel(
        NewsArticle article,
        ILocalizationService localizationService)
    {
        _article = article;
        _localizationService = localizationService;
        OpenLinkCommand = new RelayCommand(OpenLink, () => HasLink);
        ApplyLanguage();
    }

    public string Id => _announcement?.Id ?? $"article:{_article!.Id}";

    public bool IsAnnouncement => _announcement is not null;

    public bool CanDismiss => _announcement?.IsDismissible == true;

    public string Severity => _announcement?.Severity ?? "news";

    public string AccentColor => Severity.ToLowerInvariant() switch
    {
        "critical" => "#FF5570",
        "warning" => "#FFB84D",
        "maintenance" => "#29C7F2",
        "info" => "#8A5CFF",
        _ => "#10E43A"
    };

    public string BackgroundColor => Severity.ToLowerInvariant() switch
    {
        "critical" => "#241016",
        "warning" => "#241B0E",
        "maintenance" => "#0B1D27",
        "info" => "#171127",
        _ => "#10151F"
    };

    public string IconGlyph => Severity.ToLowerInvariant() switch
    {
        "critical" => "\uEA39",
        "warning" => "\uE7BA",
        "maintenance" => "\uE90F",
        "info" => "\uE946",
        _ => "\uE7F4"
    };

    public bool HasLink => IsSafeHttpsLink(Link);

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string Kind
    {
        get => _kind;
        private set => SetProperty(ref _kind, value);
    }

    public string DateDisplay
    {
        get => _dateDisplay;
        private set => SetProperty(ref _dateDisplay, value);
    }

    public ICommand OpenLinkCommand { get; }

    private string? Link => _announcement?.Link ?? _article?.Link;

    public void ApplyLanguage()
    {
        var language = _localizationService.CurrentLanguage;
        if (_announcement is not null)
        {
            Title = _announcement.Title.Resolve(language);
            Message = _announcement.Message.Resolve(language);
            Kind = _localizationService.Get($"Notification.Severity.{NormalizeSeverity(_announcement.Severity)}");
            DateDisplay = FormatDate(_announcement.EndsAtUtc, language);
            return;
        }

        Title = _article!.Title.Resolve(language);
        Message = _article.Summary.Resolve(language);
        Kind = _localizationService.Get($"News.Category.{_article.Category}");
        DateDisplay = FormatDate(_article.PublishedAtUtc, language);
    }

    private static string NormalizeSeverity(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => "Critical",
        "warning" => "Warning",
        "maintenance" => "Maintenance",
        _ => "Info"
    };

    private static string FormatDate(DateTimeOffset value, string language)
    {
        try
        {
            return value.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.GetCultureInfo(language));
        }
        catch (CultureNotFoundException)
        {
            return value.ToLocalTime().ToString("d MMM · HH:mm", CultureInfo.InvariantCulture);
        }
    }

    private void OpenLink()
    {
        if (!IsSafeHttpsLink(Link))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(Link!) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Missing browser associations must not terminate the launcher.
        }
    }

    private static bool IsSafeHttpsLink(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
