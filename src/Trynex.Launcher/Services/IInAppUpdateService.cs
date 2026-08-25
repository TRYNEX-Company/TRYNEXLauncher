namespace Trynex.Launcher.Services;

public sealed record InAppUpdateCheckResult(string? AvailableVersion);

public interface IInAppUpdateService
{
    Task<InAppUpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default);

    bool TryLaunchBootstrapper();
}
