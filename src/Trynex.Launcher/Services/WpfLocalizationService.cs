using System.Globalization;
using System.Windows;

namespace Trynex.Launcher.Services;

public sealed class WpfLocalizationService : ILocalizationService
{
    private const string EnglishLanguage = "en-US";
    private ResourceDictionary? _selectedDictionary;

    public string CurrentLanguage { get; private set; } = EnglishLanguage;

    public event EventHandler? LanguageChanged;

    public void ApplyLanguage(string languageCode)
    {
        var normalized = NormalizeLanguage(languageCode);
        var resources = Application.Current?.Resources;

        if (resources is not null)
        {
            if (_selectedDictionary is not null)
            {
                resources.MergedDictionaries.Remove(_selectedDictionary);
                _selectedDictionary = null;
            }

            if (!string.Equals(normalized, EnglishLanguage, StringComparison.Ordinal))
            {
                _selectedDictionary = new ResourceDictionary
                {
                    Source = new Uri(
                        $"/Trynex.Launcher;component/Localization/Strings.{normalized}.xaml",
                        UriKind.RelativeOrAbsolute)
                };
                resources.MergedDictionaries.Add(_selectedDictionary);
            }
        }

        CurrentLanguage = normalized;
        var culture = CultureInfo.GetCultureInfo(normalized);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    public string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentUICulture, Get(key), arguments);
    }

    public static string NormalizeLanguage(string? languageCode)
    {
        return languageCode switch
        {
            "ru-RU" => "ru-RU",
            "uk-UA" => "uk-UA",
            "de-DE" => "de-DE",
            _ => EnglishLanguage
        };
    }
}
