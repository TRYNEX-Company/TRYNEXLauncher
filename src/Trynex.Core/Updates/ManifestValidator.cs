using Trynex.Core.Security;

namespace Trynex.Core.Updates;

public sealed class ManifestValidator
{
    public ManifestValidationResult Validate(UpdateManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<ManifestValidationError>();

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            errors.Add(new("version.required", "Manifest version is required."));
        }

        if (manifest.Files is null)
        {
            errors.Add(new("files.required", "Manifest file list is required."));
            return new(errors);
        }

        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in manifest.Files)
        {
            if (file is null)
            {
                errors.Add(new("file.required", "Manifest contains an empty file entry."));
                continue;
            }

            if (!SafePathResolver.IsSafeRelativePath(file.RelativePath))
            {
                errors.Add(new("path.unsafe", "File path is empty, rooted, or escapes the install directory.", file.RelativePath));
            }
            else if (!knownPaths.Add(NormalizePath(file.RelativePath)))
            {
                errors.Add(new("path.duplicate", "Manifest contains the same path more than once.", file.RelativePath));
            }

            if (file.Size < 0)
            {
                errors.Add(new("size.invalid", "File size cannot be negative.", file.RelativePath));
            }

            if (!IsSha256(file.Sha256))
            {
                errors.Add(new("sha256.invalid", "SHA-256 must contain exactly 64 hexadecimal characters.", file.RelativePath));
            }
        }

        return new(errors);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}
