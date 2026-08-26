using Trynex.Core.Abstractions;
using Trynex.Core.Security;

namespace Trynex.Core.Updates;

public enum FileIntegrityStatus
{
    Valid,
    Missing,
    SizeMismatch,
    HashMismatch
}

public sealed record FileIntegrityResult(
    string RelativePath,
    FileIntegrityStatus Status,
    long ExpectedSize,
    long? ActualSize);

public sealed record FileVerificationProgress(int CompletedFiles, int TotalFiles, string RelativePath);

public sealed class FileIntegrityVerifier
{
    private readonly IFileHashService _fileHashService;
    private readonly ManifestValidator _manifestValidator;

    public FileIntegrityVerifier(IFileHashService fileHashService, ManifestValidator manifestValidator)
    {
        _fileHashService = fileHashService;
        _manifestValidator = manifestValidator;
    }

    public async Task<IReadOnlyList<FileIntegrityResult>> VerifyAsync(
        string installDirectory,
        UpdateManifest manifest,
        IProgress<FileVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = _manifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            throw new ArgumentException("Cannot verify files using an invalid update manifest.", nameof(manifest));
        }

        var results = new List<FileIntegrityResult>(manifest.Files.Count);

        for (var index = 0; index < manifest.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = manifest.Files[index];
            var fullPath = SafePathResolver.ResolveInsideRoot(installDirectory, entry.RelativePath);
            var status = FileIntegrityStatus.Valid;
            long? actualSize = null;

            if (!File.Exists(fullPath))
            {
                status = FileIntegrityStatus.Missing;
            }
            else
            {
                var fileInfo = new FileInfo(fullPath);
                actualSize = fileInfo.Length;

                if (actualSize != entry.Size)
                {
                    status = FileIntegrityStatus.SizeMismatch;
                }
                else
                {
                    var actualHash = await _fileHashService
                        .ComputeSha256Async(fullPath, cancellationToken)
                        .ConfigureAwait(false);

                    if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        status = FileIntegrityStatus.HashMismatch;
                    }
                }
            }

            results.Add(new(entry.RelativePath, status, entry.Size, actualSize));
            progress?.Report(new(index + 1, manifest.Files.Count, entry.RelativePath));
        }

        return results;
    }
}
