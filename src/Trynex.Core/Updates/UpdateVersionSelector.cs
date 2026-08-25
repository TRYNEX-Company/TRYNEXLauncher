namespace Trynex.Core.Updates;

public static class UpdateVersionSelector
{
    public static bool IsNewer(string installedVersion, string availableVersion)
    {
        if (!SemanticVersion.TryParse(installedVersion, out var installed))
        {
            throw new ArgumentException("The installed version is invalid.", nameof(installedVersion));
        }

        if (!SemanticVersion.TryParse(availableVersion, out var available))
        {
            throw new ArgumentException("The available version is invalid.", nameof(availableVersion));
        }

        return available!.CompareTo(installed) > 0;
    }
}
