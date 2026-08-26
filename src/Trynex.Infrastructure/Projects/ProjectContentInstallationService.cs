using Trynex.Core.Abstractions;
using Trynex.Core.Projects;
using Trynex.Core.Security;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Projects;

public sealed class ProjectContentInstallationService : IProjectContentInstaller
{
    private readonly IUpdatePackageDownloader _downloader;
    private readonly IFileHashService _fileHashService;
    private readonly ProjectManifestValidator _projectValidator;

    public ProjectContentInstallationService(
        IUpdatePackageDownloader downloader,
        IFileHashService fileHashService,
        ProjectManifestValidator? projectValidator = null)
    {
        _downloader = downloader;
        _fileHashService = fileHashService;
        _projectValidator = projectValidator ?? new();
    }

    public async Task<ProjectInstallResult> SynchronizeAsync(
        Uri downloadBaseUri,
        ProjectManifest project,
        string libraryRoot,
        IProgress<ProjectInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryRoot);
        var normalizedDownloadBaseUri = NormalizeDownloadBaseUri(downloadBaseUri);

        var validation = _projectValidator.Validate(project);
        if (!validation.IsValid)
        {
            throw new ArgumentException("Cannot install content using an invalid project manifest.", nameof(project));
        }

        var projectDirectory = SafePathResolver.ResolveInsideRoot(libraryRoot, project.Id);
        Directory.CreateDirectory(projectDirectory);

        progress?.Report(new(
            project.Id,
            ProjectInstallStage.Verifying,
            null,
            0,
            project.Files.Count,
            0,
            0,
            0));

        var integrityManifest = new UpdateManifest(
            project.Version,
            DateTimeOffset.UnixEpoch,
            project.Files
                .Select(file => new FileManifestEntry(file.RelativePath, file.Size, file.Sha256))
                .ToArray());
        var verifier = new FileIntegrityVerifier(_fileHashService, new ManifestValidator());
        var verificationProgress = new Progress<FileVerificationProgress>(value =>
            progress?.Report(new(
                project.Id,
                ProjectInstallStage.Verifying,
                value.RelativePath,
                value.CompletedFiles,
                value.TotalFiles,
                0,
                0,
                0)));
        var integrityResults = await verifier
            .VerifyAsync(projectDirectory, integrityManifest, verificationProgress, cancellationToken)
            .ConfigureAwait(false);

        var invalidPaths = integrityResults
            .Where(result => result.Status != FileIntegrityStatus.Valid)
            .Select(result => result.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingFiles = project.Files
            .Where(file => invalidPaths.Contains(file.RelativePath))
            .ToArray();
        var totalBytes = pendingFiles.Sum(file => file.Size);
        var completedBytes = 0L;
        var completedFiles = project.Files.Count - pendingFiles.Length;

        foreach (var file in pendingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destinationPath = SafePathResolver.ResolveInsideRoot(projectDirectory, file.RelativePath);

            if (file.Size == 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                await File.WriteAllBytesAsync(destinationPath, [], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var sourceUri = CreateObjectUri(normalizedDownloadBaseUri, project.ContentRoot, file.SourcePath);
                var fileProgress = new Progress<UpdateDownloadProgress>(value =>
                    progress?.Report(new(
                        project.Id,
                        ProjectInstallStage.Downloading,
                        file.RelativePath,
                        completedFiles,
                        project.Files.Count,
                        completedBytes + value.BytesReceived,
                        totalBytes,
                        value.BytesPerSecond)));

                await _downloader
                    .DownloadAsync(sourceUri, destinationPath, file.Size, fileProgress, cancellationToken)
                    .ConfigureAwait(false);
            }

            var actualHash = await _fileHashService
                .ComputeSha256Async(destinationPath, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destinationPath);
                throw new InvalidDataException($"Downloaded project file failed SHA-256 verification: {file.RelativePath}");
            }

            completedBytes += file.Size;
            completedFiles++;
            progress?.Report(new(
                project.Id,
                ProjectInstallStage.Downloading,
                file.RelativePath,
                completedFiles,
                project.Files.Count,
                completedBytes,
                totalBytes,
                0));
        }

        progress?.Report(new(
            project.Id,
            ProjectInstallStage.Complete,
            null,
            project.Files.Count,
            project.Files.Count,
            totalBytes,
            totalBytes,
            0));

        return new(
            project.Id,
            project.Version,
            project.Files.Count - pendingFiles.Length,
            pendingFiles.Length,
            totalBytes);
    }

    private static Uri NormalizeDownloadBaseUri(Uri downloadBaseUri)
    {
        ArgumentNullException.ThrowIfNull(downloadBaseUri);
        if (!downloadBaseUri.IsAbsoluteUri ||
            !string.Equals(downloadBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Project content base address must be an absolute HTTPS URL.", nameof(downloadBaseUri));
        }

        return downloadBaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? downloadBaseUri
            : new Uri(downloadBaseUri.AbsoluteUri + "/", UriKind.Absolute);
    }

    private static Uri CreateObjectUri(Uri downloadBaseUri, string contentRoot, string sourcePath)
    {
        var sourceUri = new Uri(downloadBaseUri, contentRoot + sourcePath);
        if (!string.Equals(sourceUri.Scheme, downloadBaseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceUri.Host, downloadBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            sourceUri.Port != downloadBaseUri.Port)
        {
            throw new InvalidDataException("Project content path changed the trusted download origin.");
        }

        return sourceUri;
    }
}
