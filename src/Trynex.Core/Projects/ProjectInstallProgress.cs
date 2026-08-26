namespace Trynex.Core.Projects;

public enum ProjectInstallStage
{
    Verifying,
    Downloading,
    Complete
}

public sealed record ProjectInstallProgress(
    string ProjectId,
    ProjectInstallStage Stage,
    string? RelativePath,
    int CompletedFiles,
    int TotalFiles,
    long BytesReceived,
    long TotalBytes,
    double BytesPerSecond)
{
    public double Percentage => TotalBytes <= 0
        ? Stage == ProjectInstallStage.Complete ? 100 : 0
        : Math.Clamp((double)BytesReceived / TotalBytes * 100, 0, 100);
}

public sealed record ProjectInstallResult(
    string ProjectId,
    string Version,
    int AlreadyValidFiles,
    int DownloadedFiles,
    long DownloadedBytes);
