using System.Collections.ObjectModel;
using System.Windows.Input;
using Trynex.Core.News;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class ProjectDetailViewModel : PageViewModel
{
    private readonly GameCardViewModel _project;
    private readonly ILocalizationService _localizationService;
    private NewsFeed? _newsFeed;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _status = string.Empty;

    public ProjectDetailViewModel(
        GameCardViewModel project,
        Action<string> navigate,
        ILocalizationService localizationService)
        : base("project", project.Name)
    {
        _project = project;
        _localizationService = localizationService;
        BackCommand = new RelayCommand(() => navigate("library"));
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
        ApplyLocalization();
    }

    public ObservableCollection<NewsCardViewModel> News { get; } = [];

    public string Id => _project.Id;

    public string Platform => _project.Platform;

    public string ArtworkPath => _project.ArtworkPath;

    public string StatusColor => _project.StatusColor;

    public string Version => _project.Version;

    public bool CanPlay => false;

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        private set => SetProperty(ref _description, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public ICommand BackCommand { get; }

    public void SetNews(NewsFeed? feed)
    {
        _newsFeed = feed;
        RefreshNews();
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        RefreshNews();
    }

    private void ApplyLocalization()
    {
        _project.ApplyLanguage(_localizationService.CurrentLanguage);
        Name = _project.Name;
        Description = _project.Description;
        Status = _project.Status;
        Title = Name;
    }

    private void RefreshNews()
    {
        News.Clear();
        if (_newsFeed?.Articles is null)
        {
            return;
        }

        foreach (var article in _newsFeed.Articles
                     .Where(article => string.Equals(article.ProjectId, Id, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(article => article.PublishedAtUtc)
                     .Take(3))
        {
            News.Add(new NewsCardViewModel(article, _localizationService));
        }
    }
}
