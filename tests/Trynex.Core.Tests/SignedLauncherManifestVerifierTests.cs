using Trynex.Core.Abstractions;
using Trynex.Core.Updates;

namespace Trynex.Core.Tests;

public sealed class SignedLauncherManifestVerifierTests
{
    [Fact]
    public void Verify_RejectsUntrustedSignature()
    {
        var verifier = new SignedLauncherManifestVerifier(
            new RejectingSignatureVerifier(),
            new LauncherUpdateManifestValidator());

        var result = verifier.Verify(CreateManifest());

        Assert.Contains(result.Errors, error => error.Code == "signature.verification.failed");
    }

    [Fact]
    public void SigningPayload_DoesNotChangeWhenSignatureChanges()
    {
        var manifest = CreateManifest();

        var first = ManifestSigningPayload.Create(manifest);
        var second = ManifestSigningPayload.Create(manifest with { Signature = Convert.ToBase64String([9, 9, 9]) });

        Assert.Equal(first, second);
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
            Convert.ToBase64String([1, 2, 3]));
    }

    private sealed class RejectingSignatureVerifier : IManifestSignatureVerifier
    {
        public bool Verify(ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> signature) => false;
    }
}
