using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Trynex.Core.News;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class NewsCardViewModel : ObservableObject
{
    private readonly NewsArticle _article;
    private readonly ILocalizationService _localizationService;
    private string _title = string.Empty;
    private string _summary = string.Empty;
    private string _category = string.Empty;
    private string _dateDisplay = string.Empty;

    public NewsCardViewModel(NewsArticle article, ILocalizationService localizationService)
    {
        _article = article;
        _localizationService = localizationService;
        OpenLinkCommand = new RelayCommand(OpenLink, () => HasLink);
        ApplyLanguage();
    }

    public string Id => _article.Id;

    public string? ProjectId => _article.ProjectId;

    public bool IsFeatured => _article.IsFeatured;

    public string ArtworkPath => _article.ArtworkPath;

    public bool HasLink => IsSafeHttpsLink(_article.Link);

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string Category
    {
        get => _category;
        private set => SetProperty(ref _category, value);
    }

    public string DateDisplay
    {
        get => _dateDisplay;
        private set => SetProperty(ref _dateDisplay, value);
    }

    public ICommand OpenLinkCommand { get; }

    public void ApplyLanguage()
    {
        var language = _localizationService.CurrentLanguage;
        Title = _article.Title.Resolve(language);
        Summary = _article.Summary.Resolve(language);
        Category = _localizationService.Get($"News.Category.{_article.Category}");

        try
        {
            DateDisplay = _article.PublishedAtUtc.ToLocalTime().ToString(
                "d MMM yyyy",
                CultureInfo.GetCultureInfo(language));
        }
        catch (CultureNotFoundException)
        {
            DateDisplay = _article.PublishedAtUtc.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }
    }

    private void OpenLink()
    {
        if (!IsSafeHttpsLink(_article.Link))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_article.Link!) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // A missing browser association must not terminate the launcher.
        }
    }

    private static bool IsSafeHttpsLink(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
