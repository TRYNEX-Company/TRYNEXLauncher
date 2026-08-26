using System.Text.Json;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Updates;

public sealed class JsonLauncherInstallStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _statePath;

    public JsonLauncherInstallStateStore(string statePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        _statePath = Path.GetFullPath(statePath);
    }

    public async Task<LauncherInstallState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            return new();
        }

        try
        {
            await using var stream = new FileStream(
                _statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var state = await JsonSerializer
                .DeserializeAsync<LauncherInstallState>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return Normalize(state ?? new());
        }
        catch (JsonException)
        {
            return new();
        }
        catch (IOException)
        {
            return new();
        }
        catch (UnauthorizedAccessException)
        {
            return new();
        }
    }

    public async Task SaveAsync(
        LauncherInstallState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var normalized = Normalize(state);
        var directory = Path.GetDirectoryName(_statePath)!;
        Directory.CreateDirectory(directory);

        var temporaryPath = _statePath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         16 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer
                .SerializeAsync(stream, normalized, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _statePath, true);
    }

    private static LauncherInstallState Normalize(LauncherInstallState state)
    {
        return new(
            NormalizeVersion(state.ActiveVersion),
            NormalizeVersion(state.PreviousVersion),
            NormalizeVersion(state.PendingVersion),
            NormalizeVersion(state.FailedVersion));
    }

    private static string? NormalizeVersion(string? value)
    {
        return SemanticVersion.TryParse(value, out _)
            ? value
            : null;
    }
}
