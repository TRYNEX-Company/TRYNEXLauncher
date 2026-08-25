using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Trynex.Core.News;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class HomeViewModel : PageViewModel
{
    private readonly ILocalizationService _localizationService;
    private NewsFeed? _newsFeed;

    public HomeViewModel(
        Action<string> navigate,
        string title = "Главная",
        ILocalizationService? localizationService = null)
        : base("home", title)
    {
        _localizationService = localizationService ?? new FallbackLocalizationService();
        OpenLibraryCommand = new RelayCommand(() => navigate("library"));
        OpenDownloadsCommand = new RelayCommand(() => navigate("downloads"));
        OpenCommunityCommand = new RelayCommand(() => navigate("community"));
        OpenMrProjectCommand = new RelayCommand(() => navigate("project:mr-project"));
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public ObservableCollection<NewsCardViewModel> News { get; } = [];

    public ICommand OpenLibraryCommand { get; }

    public ICommand OpenDownloadsCommand { get; }

    public ICommand OpenCommunityCommand { get; }

    public ICommand OpenMrProjectCommand { get; }

    public string VersionDisplay { get; } = CreateVersionDisplay();

    public void SetNews(NewsFeed? feed)
    {
        _newsFeed = feed;
        RefreshNews();
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e) => RefreshNews();

    private void RefreshNews()
    {
        News.Clear();
        if (_newsFeed?.Articles is null)
        {
            return;
        }

        foreach (var article in _newsFeed.Articles
                     .OrderByDescending(article => article.IsFeatured)
                     .ThenByDescending(article => article.PublishedAtUtc)
                     .Take(3))
        {
            News.Add(new NewsCardViewModel(article, _localizationService));
        }
    }

    private static string CreateVersionDisplay()
    {
        var informationalVersion = typeof(HomeViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? "0.0.0-local"
            : informationalVersion.Split('+', 2)[0];

        return version.Replace("-", " · ", StringComparison.Ordinal).ToUpperInvariant();
    }
}
