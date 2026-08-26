using System.Text.Json;
using Trynex.Core.Abstractions;
using Trynex.Core.Settings;

namespace Trynex.Infrastructure.Settings;

public sealed class JsonLauncherSettingsStore : ILauncherSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonLauncherSettingsStore(string? settingsPath = null)
    {
        _settingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : Path.GetFullPath(settingsPath);
    }

    public async Task<LauncherSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new LauncherSettings();
        }

        try
        {
            await using var stream = new FileStream(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var settings = await JsonSerializer
                .DeserializeAsync<LauncherSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return (settings ?? new LauncherSettings()).Normalize();
        }
        catch (JsonException)
        {
            return new LauncherSettings();
        }
        catch (IOException)
        {
            return new LauncherSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new LauncherSettings();
        }
    }

    public async Task SaveAsync(LauncherSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("Settings path must have a parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(_settingsPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer
                    .SerializeAsync(stream, settings.Normalize(), SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TRYNEX",
            "settings.json");
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
            // A stale temporary settings file is safe and can be cleaned on a later maintenance pass.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not hide the original save failure with a best-effort cleanup error.
        }
    }
}
