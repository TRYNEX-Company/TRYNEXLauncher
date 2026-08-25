namespace Trynex.Core.Updates;

public sealed record UpdateManifest(
    string Version,
    DateTimeOffset PublishedAtUtc,
    IReadOnlyList<FileManifestEntry> Files);
