using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Trynex.Core.Updates;
using Trynex.Infrastructure.Networking;
using Trynex.Infrastructure.Security;
using Trynex.Infrastructure.Updates;

namespace Trynex.Launcher.Services;

public sealed class InAppUpdateService : IInAppUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Uri _manifestUri;
    private readonly string _publicKeyPem;

    private InAppUpdateService(HttpClient httpClient, Uri manifestUri, string publicKeyPem)
    {
        _httpClient = httpClient;
        _manifestUri = manifestUri;
        _publicKeyPem = publicKeyPem;
    }

    public static bool TryCreateDefault(out InAppUpdateService? service)
    {
        service = null;
        if (!LauncherUpdateTrustConfiguration.TryCreate(out var downloadBaseUri, out var publicKeyPem))
        {
            return false;
        }

        var httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TRYNEX-Launcher", GetLauncherProductVersion()));

        service = new InAppUpdateService(
            httpClient,
            new Uri(downloadBaseUri!, LauncherUpdateTrustConfiguration.ManifestObjectPath),
            publicKeyPem!);
        return true;
    }

    public async Task<InAppUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        var manifestClient = new JsonUpdateManifestClient(_httpClient);
        var manifest = await manifestClient
            .GetAsync(_manifestUri, cancellationToken)
            .ConfigureAwait(false);

        using var signatureVerifier = new EcdsaManifestSignatureVerifier(_publicKeyPem);
        var verifier = new SignedLauncherManifestVerifier(
            signatureVerifier,
            new LauncherUpdateManifestValidator());
        var verification = verifier.Verify(manifest);
        if (!verification.IsValid ||
            !string.Equals(manifest.Channel, "preview", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The R2 update manifest is not trusted for the preview channel.");
        }

        if (manifest.MinimumBootstrapperVersion is not null &&
            UpdateVersionSelector.IsNewer(
                LauncherUpdateTrustConfiguration.BootstrapperVersion,
                manifest.MinimumBootstrapperVersion))
        {
            throw new InvalidDataException("The update requires a newer trusted bootstrapper.");
        }

        return new InAppUpdateCheckResult(
            UpdateVersionSelector.IsNewer(currentVersion, manifest.Version)
                ? manifest.Version
                : null);
    }

    public bool TryLaunchBootstrapper()
    {
        var bootstrapperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TRYNEX",
            "App",
            "TRYNEX.exe");
        if (!File.Exists(bootstrapperPath))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = bootstrapperPath,
                WorkingDirectory = Path.GetDirectoryName(bootstrapperPath)!,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static string GetLauncherProductVersion()
    {
        var version = typeof(InAppUpdateService).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
