using Trynex.Core.Settings;

namespace Trynex.Core.Tests;

public sealed class LauncherSettingsTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(250, 250)]
    [InlineData(5000, 1000)]
    public void Normalize_ClampsDownloadLimit(int input, int expected)
    {
        var settings = new LauncherSettings { DownloadLimitMbps = input };

        Assert.Equal(expected, settings.Normalize().DownloadLimitMbps);
    }

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("uk-UA")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Normalize_PreservesSupportedLanguage(string language)
    {
        var settings = new LauncherSettings { Language = language };

        Assert.Equal(language, settings.Normalize().Language);
    }

    [Fact]
    public void Normalize_FallsBackToEnglish_ForUnknownLanguage()
    {
        var settings = new LauncherSettings { Language = "unknown" };

        Assert.Equal("en-US", settings.Normalize().Language);
    }
}
