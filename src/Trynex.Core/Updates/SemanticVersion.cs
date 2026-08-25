using System.Globalization;
using System.Text.RegularExpressions;

namespace Trynex.Core.Updates;

public sealed record SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PreReleaseIdentifiers) : IComparable<SemanticVersion>
{
    private static readonly Regex Pattern = new(
        "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-([0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public bool IsPrerelease => PreReleaseIdentifiers.Count > 0;

    public static bool TryParse(string? value, out SemanticVersion? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        var match = Pattern.Match(value);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(match.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        var prerelease = match.Groups[4].Success
            ? match.Groups[4].Value.Split('.', StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        if (prerelease.Any(identifier =>
                identifier.All(char.IsDigit) && identifier.Length > 1 && identifier[0] == '0'))
        {
            return false;
        }

        version = new(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var coreComparison = Major.CompareTo(other.Major);
        if (coreComparison == 0)
        {
            coreComparison = Minor.CompareTo(other.Minor);
        }

        if (coreComparison == 0)
        {
            coreComparison = Patch.CompareTo(other.Patch);
        }

        if (coreComparison != 0)
        {
            return coreComparison;
        }

        if (!IsPrerelease || !other.IsPrerelease)
        {
            return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        }

        var identifierCount = Math.Min(PreReleaseIdentifiers.Count, other.PreReleaseIdentifiers.Count);
        for (var index = 0; index < identifierCount; index++)
        {
            var comparison = CompareIdentifier(
                PreReleaseIdentifiers[index],
                other.PreReleaseIdentifiers[index]);

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return PreReleaseIdentifiers.Count.CompareTo(other.PreReleaseIdentifiers.Count);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftIsNumeric && rightIsNumeric)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftIsNumeric != rightIsNumeric)
        {
            return leftIsNumeric ? -1 : 1;
        }

        return string.CompareOrdinal(left, right);
    }
}
