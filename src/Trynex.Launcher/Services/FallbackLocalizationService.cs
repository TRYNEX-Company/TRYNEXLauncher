namespace Trynex.Launcher.Services;

internal sealed class FallbackLocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> RussianStrings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Nav.Home"] = "Главная",
            ["Nav.Library"] = "Библиотека",
            ["Nav.Downloads"] = "Загрузки",
            ["Nav.Community"] = "Сообщество",
            ["Nav.Messenger"] = "Мессенджер",
            ["Nav.Settings"] = "Настройки",
            ["Common.Soon"] = "СКОРО",
            ["Update.Button.Default"] = "ОБНОВИТЬ TRYNEX",
            ["Update.Button.Version"] = "ОБНОВИТЬ ДО {0}",
            ["Update.Button.Failed"] = "НЕ УДАЛОСЬ ЗАПУСТИТЬ ОБНОВЛЕНИЕ",
            ["Settings.Status.LocalOnly"] = "Настройки сохраняются только на этом устройстве.",
            ["Settings.Status.Saving"] = "Сохраняем…",
            ["Settings.Status.Saved"] = "Сохранено · {0}",
            ["Settings.Status.IoError"] = "Не удалось сохранить настройки. Проверь доступ к папке.",
            ["Settings.Status.AccessError"] = "Windows запретила запись настроек для этого пользователя.",
            ["Library.Mr.Description"] = "Проект переносится на Arma Reforger. Поддержка модов и подключение к серверу появятся после утверждения серверной сборки.",
            ["Library.Mr.Status"] = "ПЕРЕХОД НА REFORGER",
            ["Notification.Center.Title"] = "Уведомления",
            ["Notification.Center.Subtitle"] = "Важные события, техработы и новости TRYNEX",
            ["Notification.Center.Empty"] = "Новых уведомлений пока нет",
            ["Notification.ReadMore"] = "ПОДРОБНЕЕ →",
            ["Notification.Dismiss"] = "Скрыть уведомление",
            ["Notification.Severity.Info"] = "ИНФОРМАЦИЯ",
            ["Notification.Severity.Maintenance"] = "ТЕХНИЧЕСКИЕ РАБОТЫ",
            ["Notification.Severity.Warning"] = "ПРЕДУПРЕЖДЕНИЕ",
            ["Notification.Severity.Critical"] = "ВАЖНО",
            ["News.Category.Launcher"] = "ЛАУНЧЕР",
            ["News.Category.Project"] = "ПРОЕКТ",
            ["News.Category.Ecosystem"] = "ЭКОСИСТЕМА"
        };

    public string CurrentLanguage { get; private set; } = "ru-RU";

    public event EventHandler? LanguageChanged;

    public void ApplyLanguage(string languageCode)
    {
        CurrentLanguage = WpfLocalizationService.NormalizeLanguage(languageCode);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key) => RussianStrings.TryGetValue(key, out var value) ? value : key;

    public string Format(string key, params object?[] arguments) => string.Format(Get(key), arguments);
}
