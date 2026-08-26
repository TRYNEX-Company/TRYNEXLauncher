namespace Trynex.Launcher.ViewModels;

public sealed class DownloadsViewModel : PageViewModel
{
    public DownloadsViewModel(string title = "Загрузки")
        : base("downloads", title)
    {
    }

    public int ActiveDownloads => 0;
}
