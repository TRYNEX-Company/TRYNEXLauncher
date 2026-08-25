namespace Trynex.Core.Updates;

public sealed record LauncherUpdateManifest(
    int SchemaVersion,
    string Product,
    string Channel,
    string Version,
    DateTimeOffset PublishedAtUtc,
    string PackagePath,
    long PackageSize,
    string PackageSha256,
    string Signature,
    string? MinimumBootstrapperVersion = null,
    bool Mandatory = false);
