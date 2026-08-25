using Trynex.Core.Abstractions;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Updates;

public sealed class UpdatePackageAcquisitionService
{
    private readonly IFileHashService _fileHashService;
    private readonly IUpdatePackageDownloader _packageDownloader;
    private readonly SignedLauncherManifestVerifier _signedManifestVerifier;

    public UpdatePackageAcquisitionService(
        IUpdatePackageDownloader packageDownloader,
        IFileHashService fileHashService,
        SignedLauncherManifestVerifier signedManifestVerifier)
    {
        _packageDownloader = packageDownloader;
        _fileHashService = fileHashService;
        _signedManifestVerifier = signedManifestVerifier;
    }

    public async Task<string> AcquireAsync(
        Uri downloadBaseUri,
        LauncherUpdateManifest manifest,
        string stagingDirectory,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHttpsBaseUri(downloadBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);

        var verification = _signedManifestVerifier.Verify(manifest);
        if (!verification.IsValid)
        {
            throw new InvalidDataException("The launcher update manifest is invalid or untrusted.");
        }

        var packageUri = new Uri(downloadBaseUri, manifest.PackagePath);
        if (!HasSameOrigin(downloadBaseUri, packageUri))
        {
            throw new InvalidDataException("The update package resolved outside the trusted R2 origin.");
        }

        Directory.CreateDirectory(stagingDirectory);
        var destinationPath = Path.Combine(
            Path.GetFullPath(stagingDirectory),
            $"launcher-{manifest.Version}.zip");

        await _packageDownloader
            .DownloadAsync(packageUri, destinationPath, manifest.PackageSize, progress, cancellationToken)
            .ConfigureAwait(false);

        var actualHash = await _fileHashService
            .ComputeSha256Async(destinationPath, cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(actualHash, manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);
            throw new InvalidDataException("The downloaded package does not match its signed SHA-256 hash.");
        }

        return destinationPath;
    }

    private static void EnsureHttpsBaseUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The update base URI must be an absolute HTTPS URI ending with a slash.",
                nameof(uri));
        }
    }

    private static bool HasSameOrigin(Uri trustedBase, Uri candidate)
    {
        return string.Equals(trustedBase.Scheme, candidate.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(trustedBase.Host, candidate.Host, StringComparison.OrdinalIgnoreCase)
            && trustedBase.Port == candidate.Port;
    }
}
