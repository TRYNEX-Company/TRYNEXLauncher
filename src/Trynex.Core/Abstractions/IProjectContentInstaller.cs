using Trynex.Core.Projects;

namespace Trynex.Core.Abstractions;

public interface IProjectContentInstaller
{
    Task<ProjectInstallResult> SynchronizeAsync(
        Uri downloadBaseUri,
        ProjectManifest project,
        string libraryRoot,
        IProgress<ProjectInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
