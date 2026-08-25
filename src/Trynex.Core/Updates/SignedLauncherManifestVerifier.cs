using Trynex.Core.Abstractions;

namespace Trynex.Core.Updates;

public sealed class SignedLauncherManifestVerifier
{
    private readonly IManifestSignatureVerifier _signatureVerifier;
    private readonly LauncherUpdateManifestValidator _validator;

    public SignedLauncherManifestVerifier(
        IManifestSignatureVerifier signatureVerifier,
        LauncherUpdateManifestValidator validator)
    {
        _signatureVerifier = signatureVerifier;
        _validator = validator;
    }

    public ManifestValidationResult Verify(LauncherUpdateManifest manifest)
    {
        var validation = _validator.Validate(manifest);
        if (!validation.IsValid)
        {
            return validation;
        }

        var payload = ManifestSigningPayload.Create(manifest);
        var signature = Convert.FromBase64String(manifest.Signature);

        if (_signatureVerifier.Verify(payload, signature))
        {
            return validation;
        }

        return new([
            .. validation.Errors,
            new("signature.verification.failed", "The update manifest signature is not trusted.")
        ]);
    }
}
