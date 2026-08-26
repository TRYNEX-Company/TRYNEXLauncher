using Trynex.Launcher.Presentation;

namespace Trynex.Launcher.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    private string _title;

    protected PageViewModel(string key, string title)
    {
        Key = key;
        _title = title;
    }

    public string Key { get; }

    public string Title
    {
        get => _title;
        internal set => SetProperty(ref _title, value);
    }
}
