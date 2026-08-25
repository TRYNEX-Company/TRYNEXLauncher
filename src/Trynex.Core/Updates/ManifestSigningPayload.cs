using System.Globalization;
using System.Text;

namespace Trynex.Core.Updates;

public static class ManifestSigningPayload
{
    public static byte[] Create(LauncherUpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var lines = new[]
        {
            $"schemaVersion={manifest.SchemaVersion.ToString(CultureInfo.InvariantCulture)}",
            $"product={manifest.Product}",
            $"channel={manifest.Channel.ToLowerInvariant()}",
            $"version={manifest.Version}",
            $"publishedAtUtc={manifest.PublishedAtUtc.ToUniversalTime():O}",
            $"packagePath={manifest.PackagePath}",
            $"packageSize={manifest.PackageSize.ToString(CultureInfo.InvariantCulture)}",
            $"packageSha256={manifest.PackageSha256.ToLowerInvariant()}",
            $"minimumBootstrapperVersion={manifest.MinimumBootstrapperVersion ?? string.Empty}",
            $"mandatory={manifest.Mandatory.ToString(CultureInfo.InvariantCulture).ToLowerInvariant()}"
        };

        return Encoding.UTF8.GetBytes(string.Join('\n', lines));
    }
}
