namespace Trynex.Core.Settings;

public sealed record LauncherSettings
{
    public string Language { get; init; } = "ru-RU";

    public string DefaultInstallDirectory { get; init; } = string.Empty;

    public bool StartWithWindows { get; init; }

    public bool MinimizeToTray { get; init; } = true;

    public bool AllowPrereleaseUpdates { get; init; }

    /// <summary>
    /// Zero means that download speed is not limited.
    /// </summary>
    public int DownloadLimitMbps { get; init; }

    public LauncherSettings Normalize()
    {
        var normalizedLanguage = Language switch
        {
            "ru-RU" => "ru-RU",
            "en-US" => "en-US",
            "uk-UA" => "uk-UA",
            "de-DE" => "de-DE",
            _ => "en-US"
        };

        return this with
        {
            Language = normalizedLanguage,
            DefaultInstallDirectory = DefaultInstallDirectory?.Trim() ?? string.Empty,
            DownloadLimitMbps = Math.Clamp(DownloadLimitMbps, 0, 1000)
        };
    }
}
