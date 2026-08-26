using System.IO.Compression;
using Trynex.Core.Security;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Updates;

public sealed class VersionedLauncherPackageInstaller
{
    public const string LauncherExecutableName = "Trynex.Launcher.exe";

    private const int MaximumEntries = 10_000;
    private const long MaximumExtractedBytes = 1024L * 1024 * 1024;

    public async Task<string> InstallAsync(
        string packagePath,
        string versionsDirectory,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsDirectory);

        if (!SemanticVersion.TryParse(version, out _))
        {
            throw new ArgumentException("The launcher version is invalid.", nameof(version));
        }

        var fullPackagePath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPackagePath))
        {
            throw new FileNotFoundException("The verified launcher package was not found.", fullPackagePath);
        }

        var fullVersionsDirectory = Path.GetFullPath(versionsDirectory);
        Directory.CreateDirectory(fullVersionsDirectory);

        var finalDirectory = SafePathResolver.ResolveInsideRoot(fullVersionsDirectory, version);
        var finalExecutable = Path.Combine(finalDirectory, LauncherExecutableName);
        if (File.Exists(finalExecutable))
        {
            return finalDirectory;
        }

        if (Directory.Exists(finalDirectory))
        {
            throw new InvalidDataException("The target launcher version directory is incomplete.");
        }

        var stagingName = $".staging-{Guid.NewGuid():N}";
        var stagingDirectory = SafePathResolver.ResolveInsideRoot(fullVersionsDirectory, stagingName);
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            using var archive = ZipFile.OpenRead(fullPackagePath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumEntries)
            {
                throw new InvalidDataException("The launcher package contains an invalid number of entries.");
            }

            var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extractedBytes = 0L;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = entry.FullName.Replace('\\', '/').TrimEnd('/');
                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                if (!SafePathResolver.IsSafeRelativePath(relativePath) || !knownPaths.Add(relativePath))
                {
                    throw new InvalidDataException("The launcher package contains an unsafe or duplicate path.");
                }

                if (IsSymbolicLink(entry))
                {
                    throw new InvalidDataException("Symbolic links are not allowed in launcher packages.");
                }

                extractedBytes = checked(extractedBytes + entry.Length);
                if (extractedBytes > MaximumExtractedBytes)
                {
                    throw new InvalidDataException("The launcher package expands beyond the safety limit.");
                }

                var destinationPath = SafePathResolver.ResolveInsideRoot(stagingDirectory, relativePath);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    128 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(destination, 128 * 1024, cancellationToken).ConfigureAwait(false);
            }

            if (!File.Exists(Path.Combine(stagingDirectory, LauncherExecutableName)))
            {
                throw new InvalidDataException($"The launcher package does not contain {LauncherExecutableName}.");
            }

            Directory.Move(stagingDirectory, finalDirectory);
            return finalDirectory;
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, true);
            }

            throw;
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int fileTypeMask = 0xF000;
        const int symbolicLinkType = 0xA000;
        var unixMode = entry.ExternalAttributes >> 16;
        return (unixMode & fileTypeMask) == symbolicLinkType;
    }
}
