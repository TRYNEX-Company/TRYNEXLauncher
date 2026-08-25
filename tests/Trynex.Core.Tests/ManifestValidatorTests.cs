using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class ManifestValidatorTests
{
    private const string ValidHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Validate_AcceptsWellFormedManifest()
    {
        var manifest = new UpdateManifest(
            "1.0.0",
            DateTimeOffset.UtcNow,
            [new FileManifestEntry("bin/game.exe", 128, ValidHash)]);

        var result = new ManifestValidator().Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsTraversalNegativeSizeAndBadHash()
    {
        var manifest = new UpdateManifest(
            "1.0.0",
            DateTimeOffset.UtcNow,
            [new FileManifestEntry("../escape.exe", -1, "not-a-hash")]);

        var result = new ManifestValidator().Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "path.unsafe");
        Assert.Contains(result.Errors, error => error.Code == "size.invalid");
        Assert.Contains(result.Errors, error => error.Code == "sha256.invalid");
    }

    [Fact]
    public void Validate_RejectsDuplicatePathsIgnoringCaseAndSeparators()
    {
        var manifest = new UpdateManifest(
            "1.0.0",
            DateTimeOffset.UtcNow,
            [
                new FileManifestEntry("bin/game.exe", 1, ValidHash),
                new FileManifestEntry("BIN\\GAME.EXE", 1, ValidHash)
            ]);

        var result = new ManifestValidator().Validate(manifest);

        Assert.Contains(result.Errors, error => error.Code == "path.duplicate");
    }
}
