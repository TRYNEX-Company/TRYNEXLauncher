using Trynex.Core.Updates;

namespace Trynex.Bootstrapper;

internal enum BootstrapperStage
{
    Starting,
    Checking,
    Downloading,
    Verifying,
    Installing,
    Launching,
    RollingBack,
    Ready,
    Warning
}

internal sealed record BootstrapperProgress(
    BootstrapperStage Stage,
    string Title,
    string Detail,
    double? Percentage = null,
    string? TransferText = null)
{
    public static BootstrapperProgress Starting() => new(
        BootstrapperStage.Starting,
        "Запускаем TRYNEX",
        "Подготавливаем безопасную среду запуска.");

    public static BootstrapperProgress Checking(string? installedVersion) => new(
        BootstrapperStage.Checking,
        "Проверяем обновления",
        installedVersion is null
            ? "Ищем актуальную версию лаунчера."
            : $"Установлена версия {installedVersion}. Сверяем её с R2.");

    public static BootstrapperProgress Current(string version) => new(
        BootstrapperStage.Ready,
        "Установлена актуальная версия",
        $"TRYNEX {version} готов к запуску.",
        100);

    public static BootstrapperProgress Downloading(string version, UpdateDownloadProgress value)
    {
        var received = FormatBytes(value.BytesReceived);
        var total = FormatBytes(value.TotalBytes);
        var speed = value.BytesPerSecond <= 0 ? "подключение…" : $"{FormatBytes(value.BytesPerSecond)}/с";

        return new(
            BootstrapperStage.Downloading,
            $"Скачиваем TRYNEX {version}",
            "Можно закрыть окно — загрузка продолжится с того же места при следующем запуске.",
            value.Percentage,
            $"{received} из {total}  •  {speed}");
    }

    public static BootstrapperProgress Verifying(string version) => new(
        BootstrapperStage.Verifying,
        "Проверяем обновление",
        $"Проверяем подпись и целостность пакета {version}.",
        100);

    public static BootstrapperProgress Installing(string version) => new(
        BootstrapperStage.Installing,
        "Устанавливаем обновление",
        $"Версия {version} устанавливается отдельно от рабочей версии.");

    public static BootstrapperProgress Launching(string version) => new(
        BootstrapperStage.Launching,
        "Запускаем TRYNEX",
        $"Открываем версию {version} и проверяем её первый запуск.");

    public static BootstrapperProgress Ready(string version) => new(
        BootstrapperStage.Ready,
        "TRYNEX готов",
        $"Версия {version} успешно запущена.",
        100);

    public static BootstrapperProgress Warning(string detail) => new(
        BootstrapperStage.Warning,
        "Сеть временно недоступна",
        detail);

    public static BootstrapperProgress RollingBack(string? version) => new(
        BootstrapperStage.RollingBack,
        "Возвращаем рабочую версию",
        version is null
            ? "Новая версия не подтвердила запуск."
            : $"Новая версия не подтвердила запуск. Возвращаем {version}.");

    private static string FormatBytes(double bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.0} {units[unitIndex]}";
    }
}
