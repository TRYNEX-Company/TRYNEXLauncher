using System.Runtime.ExceptionServices;
using System.Windows;
using Trynex.Core.Abstractions;
using Trynex.Core.Settings;
using Trynex.Launcher.Services;
using Trynex.Launcher.ViewModels;

namespace Trynex.Launcher.Tests;

public sealed class ShellSmokeTests
{
    [Fact]
    public void MainWindow_LoadsApplicationResourcesAndHomePage()
    {
        Exception? failure = null;
        var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var localization = new WpfLocalizationService();
                localization.ApplyLanguage("ru-RU");
                var viewModel = new MainWindowViewModel(
                    new InMemorySettingsStore(),
                    localizationService: localization);
                var window = new MainWindow { DataContext = viewModel };

                window.Measure(new Size(1440, 860));
                window.Arrange(new Rect(0, 0, 1440, 860));
                window.UpdateLayout();

                Assert.Equal("Главная", viewModel.CurrentPage.Title);
                Assert.Equal("Добро пожаловать в TRYNEX", app.TryFindResource("Home.Welcome"));
                Assert.NotNull(window.Content);

                viewModel.Settings.SelectedLanguage = "de-DE";
                window.UpdateLayout();

                Assert.Equal("Start", viewModel.PrimaryNavigation[0].Title);
                Assert.Equal("Willkommen bei TRYNEX", app.TryFindResource("Home.Welcome"));

                window.Close();
                app.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "WPF shell did not initialize within ten seconds.");
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class InMemorySettingsStore : ILauncherSettingsStore
    {
        public Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new LauncherSettings());

        public Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
