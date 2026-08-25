namespace Trynex.Core.Updates;

public sealed record LauncherInstallState(
    string? ActiveVersion = null,
    string? PreviousVersion = null,
    string? PendingVersion = null,
    string? FailedVersion = null)
{
    public LauncherInstallState Activate(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        return new(
            version,
            string.Equals(ActiveVersion, version, StringComparison.OrdinalIgnoreCase)
                ? PreviousVersion
                : ActiveVersion,
            version,
            FailedVersion);
    }

    public LauncherInstallState ConfirmHealthy()
    {
        return this with
        {
            PendingVersion = null,
            FailedVersion = null
        };
    }

    public LauncherInstallState Rollback()
    {
        if (PendingVersion is null)
        {
            return this;
        }

        return PreviousVersion is null
            ? new(null, null, null, PendingVersion)
            : new(PreviousVersion, null, null, PendingVersion);
    }

    public bool IsFailedVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return string.Equals(FailedVersion, version, StringComparison.OrdinalIgnoreCase);
    }
}
