namespace Trynex.Core.Projects;

public sealed record ProjectManifest(
    int SchemaVersion,
    string Id,
    string Version,
    GamePlatform Platform,
    LocalizedProjectText Name,
    LocalizedProjectText Description,
    LocalizedProjectText Status,
    string StatusColor,
    string ArtworkPath,
    string ContentRoot,
    ProjectLaunchProfile Launch,
    IReadOnlyList<ProjectFileEntry> Files);
