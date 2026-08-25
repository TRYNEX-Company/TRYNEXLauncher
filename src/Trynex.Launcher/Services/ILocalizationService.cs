namespace Trynex.Launcher.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    event EventHandler? LanguageChanged;

    void ApplyLanguage(string languageCode);

    string Get(string key);

    string Format(string key, params object?[] arguments);
}
