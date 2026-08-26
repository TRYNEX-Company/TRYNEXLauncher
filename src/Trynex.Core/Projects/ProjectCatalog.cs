namespace Trynex.Core.Projects;

public sealed record ProjectCatalog(
    int SchemaVersion,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<ProjectManifest> Projects);
