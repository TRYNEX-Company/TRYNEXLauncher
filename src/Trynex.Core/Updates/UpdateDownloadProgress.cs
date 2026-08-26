namespace Trynex.Core.Updates;

public sealed record UpdateDownloadProgress(
    long BytesReceived,
    long TotalBytes,
    double BytesPerSecond)
{
    public double Percentage => TotalBytes == 0
        ? 0
        : Math.Clamp((double)BytesReceived / TotalBytes * 100, 0, 100);
}
