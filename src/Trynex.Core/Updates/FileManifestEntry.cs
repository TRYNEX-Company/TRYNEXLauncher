namespace Trynex.Core.Updates;

public sealed record FileManifestEntry(
    string RelativePath,
    long Size,
    string Sha256);
