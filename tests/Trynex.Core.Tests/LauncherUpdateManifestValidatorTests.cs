using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class LauncherUpdateManifestValidatorTests
{
    private readonly LauncherUpdateManifestValidator _validator = new();

    [Fact]
    public void Validate_AcceptsWellFormedR2Manifest()
    {
        var result = _validator.Validate(CreateManifest());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("../escape.zip")]
    [InlineData("/rooted.zip")]
    [InlineData("https://example.com/update.zip")]
    [InlineData("launcher\\update.zip")]
    [InlineData("launcher/update.zip?token=secret")]
    public void Validate_RejectsUnsafePackagePath(string packagePath)
    {
        var result = _validator.Validate(CreateManifest() with { PackagePath = packagePath });

        Assert.Contains(result.Errors, error => error.Code == "package.path.unsafe");
    }

    [Fact]
    public void Validate_RejectsManifestForDifferentProduct()
    {
        var result = _validator.Validate(CreateManifest() with { Product = "OTHER.Product" });

        Assert.Contains(result.Errors, error => error.Code == "product.invalid");
    }

    private static LauncherUpdateManifest CreateManifest()
    {
        return new(
            1,
            "TRYNEX.Launcher",
            "preview",
            "0.3.0-preview.1",
            DateTimeOffset.Parse("2026-08-08T06:00:00Z"),
            "launcher/preview/0.3.0-preview.1/trynex-launcher.zip",
            1024,
            new string('a', 64),
            Convert.ToBase64String([1, 2, 3]),
            "0.1.0",
            false);
    }
}
