using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Trynex.Core.Security;
using Trynex.Core.Updates;
using Trynex.Infrastructure.Files;
using Trynex.Infrastructure.Networking;
using Trynex.Infrastructure.Security;
using Trynex.Infrastructure.Updates;

namespace Trynex.Bootstrapper;

internal sealed class BootstrapperApplication
{
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(30);

    private readonly BootstrapperLogger _logger;
    private readonly BootstrapperPaths _paths;

    public BootstrapperApplication(BootstrapperPaths paths, BootstrapperLogger logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<int> RunAsync(
        IProgress<BootstrapperProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(BootstrapperProgress.Starting());
        Directory.CreateDirectory(_paths.RootDirectory);
        var stateStore = new JsonLauncherInstallStateStore(_paths.StatePath);
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state = await ConfirmPreviousHealthMarkerAsync(stateStore, state, cancellationToken).ConfigureAwait(false);
        progress?.Report(BootstrapperProgress.Checking(state.ActiveVersion));

        if (UpdateTrustConfiguration.TryCreate(out var downloadBaseUri, out var publicKeyPem))
        {
            try
            {
                state = await TryInstallUpdateAsync(
                        stateStore,
                        state,
                        downloadBaseUri!,
                        publicKeyPem!,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.Error("Update check failed; starting the installed version.", exception);
                progress?.Report(BootstrapperProgress.Warning(
                    "Не удалось проверить R2. Запускаем последнюю установленную версию."));
            }
        }
        else
        {
            _logger.Info("R2 update trust is not configured yet; starting the bundled launcher.");
            progress?.Report(BootstrapperProgress.Warning(
                "Сервис обновлений пока не настроен. Запускаем установленную версию."));
        }

        return await LaunchActiveVersionAsync(stateStore, state, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LauncherInstallState> TryInstallUpdateAsync(
        JsonLauncherInstallStateStore stateStore,
        LauncherInstallState state,
        Uri downloadBaseUri,
        string publicKeyPem,
        IProgress<BootstrapperProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var httpClient = CreateHttpClient();
        var manifestClient = new JsonUpdateManifestClient(httpClient);
        var manifestUri = new Uri(downloadBaseUri, UpdateTrustConfiguration.ManifestObjectPath);
        var manifest = await manifestClient.GetAsync(manifestUri, cancellationToken).ConfigureAwait(false);

        using var signatureVerifier = new EcdsaManifestSignatureVerifier(publicKeyPem);
        var signedManifestVerifier = new SignedLauncherManifestVerifier(
            signatureVerifier,
            new LauncherUpdateManifestValidator());
        var verification = signedManifestVerifier.Verify(manifest);
        if (!verification.IsValid)
        {
            throw new InvalidDataException("The R2 manifest signature or structure is invalid.");
        }

        if (manifest.MinimumBootstrapperVersion is not null &&
            UpdateVersionSelector.IsNewer(
                UpdateTrustConfiguration.BootstrapperVersion,
                manifest.MinimumBootstrapperVersion))
        {
            throw new InvalidDataException("The update requires a newer trusted bootstrapper.");
        }

        var installedVersion = state.ActiveVersion ?? UpdateTrustConfiguration.BundledLauncherVersion;
        if (!UpdateVersionSelector.IsNewer(installedVersion, manifest.Version))
        {
            progress?.Report(BootstrapperProgress.Current(installedVersion));
            return state;
        }

        if (state.IsFailedVersion(manifest.Version))
        {
            _logger.Info($"Launcher {manifest.Version} is quarantined after a failed startup; waiting for a newer release.");
            progress?.Report(BootstrapperProgress.Warning(
                $"Версия {manifest.Version} помещена в карантин после неудачного запуска. Открываем рабочую версию."));
            return state;
        }

        var acquisitionService = new UpdatePackageAcquisitionService(
            new ResumableUpdatePackageDownloader(httpClient),
            new Sha256FileHashService(),
            signedManifestVerifier);
        var downloadProgress = new SynchronousProgress<UpdateDownloadProgress>(value =>
        {
            progress?.Report(value.BytesReceived >= value.TotalBytes
                ? BootstrapperProgress.Verifying(manifest.Version)
                : BootstrapperProgress.Downloading(manifest.Version, value));
        });
        progress?.Report(BootstrapperProgress.Downloading(
            manifest.Version,
            new UpdateDownloadProgress(0, manifest.PackageSize, 0)));
        var packagePath = await acquisitionService
            .AcquireAsync(
                downloadBaseUri,
                manifest,
                _paths.DownloadsDirectory,
                downloadProgress,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(BootstrapperProgress.Installing(manifest.Version));
        var installer = new VersionedLauncherPackageInstaller();
        await installer
            .InstallAsync(packagePath, _paths.VersionsDirectory, manifest.Version, cancellationToken)
            .ConfigureAwait(false);

        var activated = state.Activate(manifest.Version);
        await stateStore.SaveAsync(activated, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Launcher {manifest.Version} was staged and activated.");
        return activated;
    }

    private async Task<LauncherInstallState> ConfirmPreviousHealthMarkerAsync(
        JsonLauncherInstallStateStore stateStore,
        LauncherInstallState state,
        CancellationToken cancellationToken)
    {
        if (state.PendingVersion is null)
        {
            return state;
        }

        var markerPath = GetHealthMarkerPath(state.PendingVersion);
        if (!File.Exists(markerPath))
        {
            return state;
        }

        var confirmed = state.ConfirmHealthy();
        await stateStore.SaveAsync(confirmed, cancellationToken).ConfigureAwait(false);
        File.Delete(markerPath);
        return confirmed;
    }

    private async Task<int> LaunchActiveVersionAsync(
        JsonLauncherInstallStateStore stateStore,
        LauncherInstallState state,
        IProgress<BootstrapperProgress>? progress,
        CancellationToken cancellationToken)
    {
        var launcherPath = ResolveLauncherPath(state.ActiveVersion);
        if (launcherPath is null)
        {
            _logger.Info("No installed version exists; starting the launcher beside the bootstrapper.");
            launcherPath = Path.Combine(AppContext.BaseDirectory, VersionedLauncherPackageInstaller.LauncherExecutableName);
        }

        if (!File.Exists(launcherPath))
        {
            throw new FileNotFoundException("No working TRYNEX launcher executable was found.", launcherPath);
        }

        var launchedVersion = state.ActiveVersion ?? UpdateTrustConfiguration.BundledLauncherVersion;
        progress?.Report(BootstrapperProgress.Launching(launchedVersion));

        var isPending = state.PendingVersion is not null &&
            string.Equals(state.PendingVersion, state.ActiveVersion, StringComparison.OrdinalIgnoreCase);
        var markerPath = isPending ? GetHealthMarkerPath(state.PendingVersion!) : null;

        if (markerPath is not null && File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }

        using var process = StartLauncher(launcherPath, markerPath);
        if (!isPending)
        {
            progress?.Report(BootstrapperProgress.Ready(launchedVersion));
            return 0;
        }

        var healthy = await WaitForHealthAsync(process, markerPath!, cancellationToken).ConfigureAwait(false);
        if (healthy)
        {
            await stateStore.SaveAsync(state.ConfirmHealthy(), cancellationToken).ConfigureAwait(false);
            File.Delete(markerPath!);
            _logger.Info($"Launcher {state.ActiveVersion} reported a healthy startup.");
            progress?.Report(BootstrapperProgress.Ready(launchedVersion));
            return 0;
        }

        if (!process.HasExited)
        {
            _logger.Info("The new launcher is still running but did not report health within 30 seconds.");
            progress?.Report(BootstrapperProgress.Ready(launchedVersion));
            return 0;
        }

        var rolledBack = state.Rollback();
        await stateStore.SaveAsync(rolledBack, cancellationToken).ConfigureAwait(false);
        _logger.Info($"Launcher {state.ActiveVersion} exited before health confirmation; rollback selected.");
        progress?.Report(BootstrapperProgress.RollingBack(rolledBack.ActiveVersion));

        var rollbackPath = ResolveLauncherPath(rolledBack.ActiveVersion);
        if (rollbackPath is not null && File.Exists(rollbackPath))
        {
            _ = StartLauncher(rollbackPath, null);
        }

        return 1;
    }

    private string? ResolveLauncherPath(string? version)
    {
        if (version is null || !SemanticVersion.TryParse(version, out _))
        {
            return null;
        }

        var versionDirectory = SafePathResolver.ResolveInsideRoot(_paths.VersionsDirectory, version);
        return Path.Combine(versionDirectory, VersionedLauncherPackageInstaller.LauncherExecutableName);
    }

    private string GetHealthMarkerPath(string version)
    {
        Directory.CreateDirectory(_paths.HealthDirectory);
        return SafePathResolver.ResolveInsideRoot(_paths.HealthDirectory, $"{version}.healthy");
    }

    private static Process StartLauncher(string launcherPath, string? markerPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath)!,
            UseShellExecute = false
        };

        if (markerPath is not null)
        {
            startInfo.ArgumentList.Add("--trynex-health-marker");
            startInfo.ArgumentList.Add(markerPath);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not start the TRYNEX launcher process.");
    }

    private static async Task<bool> WaitForHealthAsync(
        Process process,
        string markerPath,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + HealthTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(markerPath))
            {
                return true;
            }

            if (process.HasExited)
            {
                return false;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return File.Exists(markerPath);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TRYNEX-Bootstrapper", UpdateTrustConfiguration.BootstrapperVersion));
        return client;
    }

    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
