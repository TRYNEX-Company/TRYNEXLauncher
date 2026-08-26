namespace Trynex.Core.Projects;

public sealed record ProjectFileEntry(
    string RelativePath,
    string SourcePath,
    long Size,
    string Sha256);
