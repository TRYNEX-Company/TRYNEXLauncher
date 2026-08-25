using Trynex.Core.Security;

namespace Trynex.Core.Updates;

public sealed class LauncherUpdateManifestValidator
{
    public const int SupportedSchemaVersion = 1;
    public const string ExpectedProduct = "TRYNEX.Launcher";

    private static readonly HashSet<string> SupportedChannels =
        new(StringComparer.OrdinalIgnoreCase) { "stable", "preview" };

    public ManifestValidationResult Validate(LauncherUpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<ManifestValidationError>();

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add(new("schema.unsupported", "The launcher update manifest schema is not supported."));
        }

        if (!string.Equals(manifest.Product, ExpectedProduct, StringComparison.Ordinal))
        {
            errors.Add(new("product.invalid", "The manifest targets a different product."));
        }

        if (string.IsNullOrWhiteSpace(manifest.Channel) || !SupportedChannels.Contains(manifest.Channel))
        {
            errors.Add(new("channel.invalid", "The update channel must be stable or preview."));
        }

        if (!SemanticVersion.TryParse(manifest.Version, out _))
        {
            errors.Add(new("version.invalid", "The launcher version must be a valid semantic version."));
        }

        if (manifest.MinimumBootstrapperVersion is not null &&
            !SemanticVersion.TryParse(manifest.MinimumBootstrapperVersion, out _))
        {
            errors.Add(new("bootstrapper.version.invalid", "The minimum bootstrapper version is invalid."));
        }

        if (manifest.PublishedAtUtc == default)
        {
            errors.Add(new("publishedAt.required", "The publication timestamp is required."));
        }

        if (!IsSafeObjectPath(manifest.PackagePath))
        {
            errors.Add(new(
                "package.path.unsafe",
                "The package path must be a safe relative R2 object path.",
                manifest.PackagePath));
        }

        if (manifest.PackageSize <= 0)
        {
            errors.Add(new("package.size.invalid", "The package size must be greater than zero."));
        }

        if (!IsSha256(manifest.PackageSha256))
        {
            errors.Add(new(
                "package.sha256.invalid",
                "The package SHA-256 must contain exactly 64 hexadecimal characters."));
        }

        if (!IsBase64(manifest.Signature))
        {
            errors.Add(new("signature.invalid", "The manifest signature must be valid Base64."));
        }

        return new(errors);
    }

    private static bool IsSafeObjectPath(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Contains('\\', StringComparison.Ordinal)
            && !value.Contains('?', StringComparison.Ordinal)
            && !value.Contains('#', StringComparison.Ordinal)
            && !value.Contains('%', StringComparison.Ordinal)
            && !value.Contains("//", StringComparison.Ordinal)
            && SafePathResolver.IsSafeRelativePath(value);
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }

    private static bool IsBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            return Convert.FromBase64String(value).Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
