using Trynex.Core.Abstractions;
using Trynex.Core.Projects;
using Trynex.Launcher.Services;
using Trynex.Launcher.ViewModels;

namespace Trynex.Launcher.Tests;

public sealed class LibraryViewModelTests
{
    [Fact]
    public async Task LoadAsync_UsesCatalogAndRelocalizesProjectCard()
    {
        var localization = new FakeLocalizationService();
        var viewModel = new LibraryViewModel(localization, new FakeProjectCatalogStore());

        await viewModel.LoadAsync();
        localization.ApplyLanguage("de-DE");

        var game = Assert.Single(viewModel.Games);
        Assert.Equal("MR DE", game.Name);
        Assert.Equal("Beschreibung", game.Description);
        Assert.Equal("1.0.0", game.Version);
        Assert.Equal("ARMA REFORGER", game.Platform);
    }

    private sealed class FakeProjectCatalogStore : IProjectCatalogStore
    {
        public Task<ProjectCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectCatalog(
                1,
                DateTimeOffset.UtcNow,
                [
                    new ProjectManifest(
                        1,
                        "mr-project",
                        "1.0.0",
                        GamePlatform.ArmaReforger,
                        Text("MR EN", "MR DE"),
                        Text("Description", "Beschreibung"),
                        Text("READY", "BEREIT"),
                        "#68D9FA",
                        "mr-project.png",
                        "projects/mr-project/1.0.0/",
                        new ProjectLaunchProfile("1874880", Arguments: []),
                        [])
                ]));

        private static LocalizedProjectText Text(string english, string german) => new(
            new Dictionary<string, string>
            {
                ["en-US"] = english,
                ["de-DE"] = german
            });
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public string CurrentLanguage { get; private set; } = "en-US";

        public event EventHandler? LanguageChanged;

        public void ApplyLanguage(string languageCode)
        {
            CurrentLanguage = languageCode;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        public string Get(string key) => key;

        public string Format(string key, params object?[] arguments) => string.Format(Get(key), arguments);
    }
}
