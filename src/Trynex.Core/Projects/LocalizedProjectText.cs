namespace Trynex.Core.Projects;

public sealed record LocalizedProjectText(IReadOnlyDictionary<string, string> Values)
{
    public string Resolve(string? language, string fallbackLanguage = "en-US")
    {
        if (Values is null || Values.Count == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(language) &&
            TryGetValue(language, out var localized))
        {
            return localized;
        }

        if (TryGetValue(fallbackLanguage, out var fallback))
        {
            return fallback;
        }

        return Values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private bool TryGetValue(string language, out string value)
    {
        var match = Values.FirstOrDefault(pair =>
            string.Equals(pair.Key, language, StringComparison.OrdinalIgnoreCase));
        value = match.Value ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
