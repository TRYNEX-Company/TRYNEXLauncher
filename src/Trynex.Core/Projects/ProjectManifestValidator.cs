using System.Text.RegularExpressions;
using Trynex.Core.Security;
using Trynex.Core.Updates;

namespace Trynex.Core.Projects;

public sealed partial class ProjectManifestValidator
{
    private const int SupportedSchemaVersion = 1;

    public ManifestValidationResult Validate(ProjectManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<ManifestValidationError>();

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            errors.Add(new("schema.unsupported", "The project manifest schema is not supported."));
        }

        if (string.IsNullOrWhiteSpace(manifest.Id) || !ProjectIdPattern().IsMatch(manifest.Id))
        {
            errors.Add(new("project.id.invalid", "Project id must be a lowercase slug."));
        }

        if (!SemanticVersion.TryParse(manifest.Version, out _))
        {
            errors.Add(new("project.version.invalid", "Project version must be a semantic version."));
        }

        if (manifest.Platform == GamePlatform.Unknown)
        {
            errors.Add(new("project.platform.required", "Project game platform is required."));
        }

        ValidateLocalizedText(manifest.Name, "project.name", errors);
        ValidateLocalizedText(manifest.Description, "project.description", errors);
        ValidateLocalizedText(manifest.Status, "project.status", errors);

        if (!IsColor(manifest.StatusColor))
        {
            errors.Add(new("project.statusColor.invalid", "Status color must be #RRGGBB."));
        }

        if (string.IsNullOrWhiteSpace(manifest.ArtworkPath))
        {
            errors.Add(new("project.artwork.required", "Project artwork path is required."));
        }

        if (!IsSafeObjectDirectory(manifest.ContentRoot))
        {
            errors.Add(new("project.contentRoot.invalid", "Content root must be a safe relative object directory."));
        }

        ValidateLaunchProfile(manifest.Launch, errors);
        ValidateFiles(manifest.Files, errors);

        return new(errors);
    }

    private static void ValidateLocalizedText(
        LocalizedProjectText? text,
        string codePrefix,
        ICollection<ManifestValidationError> errors)
    {
        if (text?.Values is null || text.Values.Count == 0)
        {
            errors.Add(new($"{codePrefix}.required", "At least one localized value is required."));
            return;
        }

        if (!text.Values.Any(pair =>
                string.Equals(pair.Key, "en-US", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(pair.Value)))
        {
            errors.Add(new($"{codePrefix}.fallback", "An en-US fallback value is required."));
        }

        foreach (var pair in text.Values)
        {
            if (!CulturePattern().IsMatch(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                errors.Add(new($"{codePrefix}.invalid", "Localized values must use a valid culture key and non-empty text."));
                break;
            }
        }
    }

    private static void ValidateLaunchProfile(
        ProjectLaunchProfile? launch,
        ICollection<ManifestValidationError> errors)
    {
        if (launch is null)
        {
            errors.Add(new("project.launch.required", "Project launch profile is required."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(launch.SteamAppId) &&
            !SteamAppIdPattern().IsMatch(launch.SteamAppId))
        {
            errors.Add(new("project.launch.steamAppId.invalid", "Steam app id must contain digits only."));
        }

        if (launch.Arguments?.Any(argument => argument is null || argument.Contains('\0')) == true)
        {
            errors.Add(new("project.launch.arguments.invalid", "Launch arguments contain an invalid value."));
        }
    }

    private static void ValidateFiles(
        IReadOnlyList<ProjectFileEntry>? files,
        ICollection<ManifestValidationError> errors)
    {
        if (files is null)
        {
            errors.Add(new("project.files.required", "Project file list is required."));
            return;
        }

        var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourcePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (file is null)
            {
                errors.Add(new("project.file.required", "Project contains an empty file entry."));
                continue;
            }

            if (!SafePathResolver.IsSafeRelativePath(file.RelativePath))
            {
                errors.Add(new("project.path.unsafe", "Destination path is unsafe.", file.RelativePath));
            }
            else if (!destinationPaths.Add(NormalizePath(file.RelativePath)))
            {
                errors.Add(new("project.path.duplicate", "Destination path is duplicated.", file.RelativePath));
            }

            if (!IsSafeObjectPath(file.SourcePath))
            {
                errors.Add(new("project.sourcePath.unsafe", "Source object path is unsafe.", file.RelativePath));
            }
            else if (!sourcePaths.Add(file.SourcePath.Replace('\\', '/')))
            {
                errors.Add(new("project.sourcePath.duplicate", "Source object path is duplicated.", file.RelativePath));
            }

            if (file.Size < 0)
            {
                errors.Add(new("project.size.invalid", "File size cannot be negative.", file.RelativePath));
            }

            if (!IsSha256(file.Sha256))
            {
                errors.Add(new("project.sha256.invalid", "SHA-256 must contain 64 hexadecimal characters.", file.RelativePath));
            }
        }
    }

    internal static bool IsSafeObjectPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !path.StartsWith("/", StringComparison.Ordinal)
            && !path.EndsWith("/", StringComparison.Ordinal)
            && !path.Contains('\\')
            && !path.Contains('?')
            && !path.Contains('#')
            && !path.Contains(':')
            && SafePathResolver.IsSafeRelativePath(path);
    }

    private static bool IsSafeObjectDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.EndsWith("/", StringComparison.Ordinal)
            && IsSafeObjectPath(path.TrimEnd('/'));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private static string NormalizePath(string path) =>
        path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectIdPattern();

    [GeneratedRegex("^[a-z]{2}-[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CulturePattern();

    [GeneratedRegex("^[0-9]{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex SteamAppIdPattern();
}
