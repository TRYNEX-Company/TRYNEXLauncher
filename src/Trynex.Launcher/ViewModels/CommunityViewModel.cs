using System.Collections.ObjectModel;
using Trynex.Core.News;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class CommunityViewModel : PageViewModel
{
    private readonly ILocalizationService _localizationService;
    private NewsFeed? _newsFeed;

    public CommunityViewModel(
        string title = "Сообщество",
        ILocalizationService? localizationService = null)
        : base("community", title)
    {
        _localizationService = localizationService ?? new FallbackLocalizationService();
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public ObservableCollection<NewsCardViewModel> News { get; } = [];

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

        foreach (var article in _newsFeed.Articles.OrderByDescending(article => article.PublishedAtUtc))
        {
            News.Add(new NewsCardViewModel(article, _localizationService));
        }
    }
}
