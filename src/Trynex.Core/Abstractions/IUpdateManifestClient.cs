using Trynex.Core.Updates;

namespace Trynex.Core.Abstractions;

public interface IUpdateManifestClient
{
    Task<LauncherUpdateManifest> GetAsync(
        Uri manifestUri,
        CancellationToken cancellationToken = default);
}
