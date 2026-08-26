using Trynex.Core.Updates;

namespace Trynex.Core.Abstractions;

public interface IUpdatePackageDownloader
{
    Task DownloadAsync(
        Uri source,
        string destinationPath,
        long expectedSize,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
