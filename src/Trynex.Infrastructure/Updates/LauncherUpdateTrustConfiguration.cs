namespace Trynex.Infrastructure.Updates;

public static class LauncherUpdateTrustConfiguration
{
    public const string BootstrapperVersion = "0.2.0-preview.2";
    public const string ManifestObjectPath = "launcher/preview/manifest.json";

    // The private signing key never belongs in the repository or in a client build.
    private const string DownloadBaseUrl = "https://pub-a1a73e83cbcb452d9b855696c8f1aee4.r2.dev/";
    private const string ManifestPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAECilJCDoAPeErV+3YpmA2mr2X9qfP
        EtmUzLbU3U8OPdWXu2iDG5UHGjsM4KucuW6f1h0UiVj4VZtB4W2KWUKyiA==
        -----END PUBLIC KEY-----
        """;

    public static bool TryCreate(out Uri? downloadBaseUri, out string? publicKeyPem)
    {
        publicKeyPem = ManifestPublicKeyPem;
        return Uri.TryCreate(DownloadBaseUrl, UriKind.Absolute, out downloadBaseUri)
            && string.Equals(downloadBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && downloadBaseUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            && ManifestPublicKeyPem.Contains("BEGIN PUBLIC KEY", StringComparison.Ordinal);
    }
}
