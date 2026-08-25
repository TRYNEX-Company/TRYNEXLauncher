using System.Windows.Input;
using Trynex.Launcher.Presentation;

namespace Trynex.Launcher.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;
    private string _title;
    private string _badge;

    public NavigationItemViewModel(
        string key,
        string title,
        string iconGlyph,
        ICommand command,
        string badge = "")
    {
        Key = key;
        _title = title;
        IconGlyph = iconGlyph;
        Command = command;
        _badge = badge;
    }

    public string Key { get; }

    public string Title
    {
        get => _title;
        internal set => SetProperty(ref _title, value);
    }

    public string IconGlyph { get; }

    public string Badge
    {
        get => _badge;
        internal set => SetProperty(ref _badge, value);
    }

    public ICommand Command { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}
