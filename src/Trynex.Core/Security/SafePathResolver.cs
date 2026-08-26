namespace Trynex.Core.Security;

public static class SafePathResolver
{
    public static string ResolveInsideRoot(string rootDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath) || relativePath.Contains(':'))
        {
            throw new ArgumentException("The path must be relative and cannot contain a drive or stream specifier.", nameof(relativePath));
        }

        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || segments.Any(IsUnsafeSegment))
        {
            throw new ArgumentException("The relative path contains an unsafe segment.", nameof(relativePath));
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        var rootPrefix = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The resolved path escapes the allowed root directory.", nameof(relativePath));
        }

        return fullPath;
    }

    public static bool IsSafeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        try
        {
            _ = ResolveInsideRoot(Path.GetTempPath(), relativePath);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsUnsafeSegment(string segment)
    {
        return segment is "." or ".."
            || segment.Length == 0
            || !string.Equals(segment, segment.Trim(), StringComparison.Ordinal)
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
    }
}
