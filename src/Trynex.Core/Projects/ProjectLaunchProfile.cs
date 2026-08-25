namespace Trynex.Core.Projects;

public sealed record ProjectLaunchProfile(
    string? SteamAppId = null,
    string? ServerAddress = null,
    IReadOnlyList<string>? Arguments = null);
