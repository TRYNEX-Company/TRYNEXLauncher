using Trynex.Infrastructure.Updates;

namespace Trynex.Bootstrapper;

internal static class UpdateTrustConfiguration
{
    public const string BootstrapperVersion = LauncherUpdateTrustConfiguration.BootstrapperVersion;
    public const string BundledLauncherVersion = "0.3.0-preview.1";
    public const string ManifestObjectPath = LauncherUpdateTrustConfiguration.ManifestObjectPath;

    public static bool TryCreate(out Uri? downloadBaseUri, out string? publicKeyPem)
    {
        return LauncherUpdateTrustConfiguration.TryCreate(out downloadBaseUri, out publicKeyPem);
    }
}
