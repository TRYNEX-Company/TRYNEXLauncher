using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Updates;

public sealed class LauncherVersionRetentionService
{
    public const int MaximumRetainedVersions = 3;

    public IReadOnlyList<string> Cleanup(
        string versionsDirectory,
        LauncherInstallState state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionsDirectory);
        ArgumentNullException.ThrowIfNull(state);

        if (!SemanticVersion.TryParse(state.ActiveVersion, out var activeVersion))
        {
            return [];
        }

        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(versionsDirectory));
        if (!string.Equals(
                Path.GetFileName(rootPath),
                "versions",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The retention root must be a dedicated versions directory.",
                nameof(versionsDirectory));
        }

        var root = new DirectoryInfo(rootPath);
        if (!root.Exists || root.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return [];
        }

        var versionDirectories = new List<VersionDirectory>();
        foreach (var directory in root.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
        {
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint) ||
                !SemanticVersion.TryParse(directory.Name, out var parsedVersion))
            {
                continue;
            }

            versionDirectories.Add(new(directory, parsedVersion!));
        }

        var retained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RetainIfInstalled(retained, versionDirectories, state.ActiveVersion);
        RetainIfInstalled(retained, versionDirectories, state.PreviousVersion);
        RetainIfInstalled(retained, versionDirectories, state.PendingVersion);

        var emergencySlots = Math.Max(0, MaximumRetainedVersions - retained.Count);
        foreach (var candidate in versionDirectories
                     .Where(candidate =>
                         !retained.Contains(candidate.Directory.Name) &&
                         !string.Equals(
                             candidate.Directory.Name,
                             state.FailedVersion,
                             StringComparison.OrdinalIgnoreCase) &&
                         candidate.Version.CompareTo(activeVersion) < 0)
                     .OrderByDescending(candidate => candidate.Version)
                     .Take(emergencySlots))
        {
            retained.Add(candidate.Directory.Name);
        }

        var deleted = new List<string>();
        foreach (var candidate in versionDirectories.Where(candidate =>
                     !retained.Contains(candidate.Directory.Name)))
        {
            try
            {
                candidate.Directory.Delete(recursive: true);
                deleted.Add(candidate.Directory.Name);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // A running or locked old build can be retried on the next healthy launch.
            }
        }

        return deleted;
    }

    private static void RetainIfInstalled(
        ISet<string> retained,
        IEnumerable<VersionDirectory> installed,
        string? version)
    {
        if (version is null)
        {
            return;
        }

        var match = installed.FirstOrDefault(candidate =>
            string.Equals(candidate.Directory.Name, version, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            retained.Add(match.Directory.Name);
        }
    }

    private sealed record VersionDirectory(DirectoryInfo Directory, SemanticVersion Version);
}
