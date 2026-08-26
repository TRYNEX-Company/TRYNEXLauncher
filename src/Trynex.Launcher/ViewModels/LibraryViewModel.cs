using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Trynex.Core.Abstractions;
using Trynex.Core.Projects;
using Trynex.Launcher.Presentation;
using Trynex.Launcher.Services;

namespace Trynex.Launcher.ViewModels;

public sealed class LibraryViewModel : PageViewModel
{
    private string _searchText = string.Empty;
    private readonly ILocalizationService _localizationService;
    private readonly IProjectCatalogStore? _projectCatalogStore;

    public LibraryViewModel(
        ILocalizationService? localizationService = null,
        IProjectCatalogStore? projectCatalogStore = null)
        : base("library", "Библиотека")
    {
        _localizationService = localizationService ?? new FallbackLocalizationService();
        _projectCatalogStore = projectCatalogStore;
        Games = [CreateFallbackCard()];
        OpenProjectCommand = new RelayCommand(
            parameter => ProjectOpenRequested?.Invoke(this, (GameCardViewModel)parameter!),
            parameter => parameter is GameCardViewModel);
        _localizationService.LanguageChanged += LocalizationService_LanguageChanged;
    }

    public ObservableCollection<GameCardViewModel> Games { get; }

    public ICommand OpenProjectCommand { get; }

    public event EventHandler<GameCardViewModel>? ProjectOpenRequested;

    public IEnumerable<GameCardViewModel> FilteredGames => string.IsNullOrWhiteSpace(SearchText)
        ? Games
        : Games.Where(game =>
            game.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || game.Platform.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(FilteredGames));
            }
        }
    }

    public GameCardViewModel? FindById(string id) => Games.FirstOrDefault(game =>
        string.Equals(game.Id, id, StringComparison.OrdinalIgnoreCase));

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_projectCatalogStore is null)
        {
            return;
        }

        try
        {
            var catalog = await _projectCatalogStore.LoadAsync(cancellationToken);
            Games.Clear();
            foreach (var project in catalog.Projects)
            {
                Games.Add(CreateGameCard(project));
            }

            OnPropertyChanged(nameof(FilteredGames));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // The bundled fallback keeps the library usable if the catalog is damaged.
        }
    }

    private void LocalizationService_LanguageChanged(object? sender, EventArgs e)
    {
        foreach (var game in Games)
        {
            if (!game.ApplyLanguage(_localizationService.CurrentLanguage))
            {
                game.Description = _localizationService.Get("Library.Mr.Description");
                game.Status = _localizationService.Get("Library.Mr.Status");
            }
        }
    }

    private GameCardViewModel CreateFallbackCard() => new(
        "mr-project",
        "MR PROJECT",
        "ARMA REFORGER",
        _localizationService.Get("Library.Mr.Description"),
        _localizationService.Get("Library.Mr.Status"),
        "#68D9FA",
        "/Trynex.Launcher;component/Assets/Projects/mr-project.png",
        "0.1.0-preview.1");

    private GameCardViewModel CreateGameCard(ProjectManifest project) => new(
        project.Id,
        project.Name.Resolve(_localizationService.CurrentLanguage),
        GetPlatformName(project.Platform),
        project.Description.Resolve(_localizationService.CurrentLanguage),
        project.Status.Resolve(_localizationService.CurrentLanguage),
        project.StatusColor,
        project.ArtworkPath,
        project.Version,
        project);

    private static string GetPlatformName(GamePlatform platform) => platform switch
    {
        GamePlatform.Arma3 => "ARMA 3",
        GamePlatform.ArmaReforger => "ARMA REFORGER",
        GamePlatform.Minecraft => "MINECRAFT",
        GamePlatform.Gta5 => "GTA V",
        _ => "PC"
    };
}

public sealed class GameCardViewModel : ObservableObject
{
    private string _name;
    private string _description;
    private string _status;
    private readonly ProjectManifest? _project;

    public GameCardViewModel(
        string id,
        string name,
        string platform,
        string description,
        string status,
        string statusColor,
        string artworkPath,
        string version,
        ProjectManifest? project = null)
    {
        Id = id;
        _name = name;
        Platform = platform;
        _description = description;
        _status = status;
        StatusColor = statusColor;
        ArtworkPath = artworkPath;
        Version = version;
        _project = project;
    }

    public string Id { get; }

    public string Name
    {
        get => _name;
        private set => SetProperty(ref _name, value);
    }

    public string Platform { get; }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string StatusColor { get; }

    public string ArtworkPath { get; }

    public string Version { get; }

    public bool ApplyLanguage(string language)
    {
        if (_project is null)
        {
            return false;
        }

        Name = _project.Name.Resolve(language);
        Description = _project.Description.Resolve(language);
        Status = _project.Status.Resolve(language);
        return true;
    }
}
