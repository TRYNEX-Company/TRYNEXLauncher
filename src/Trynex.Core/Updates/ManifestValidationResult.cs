namespace Trynex.Core.Updates;

public sealed record ManifestValidationError(string Code, string Message, string? RelativePath = null);

public sealed class ManifestValidationResult
{
    public ManifestValidationResult(IReadOnlyList<ManifestValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<ManifestValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
