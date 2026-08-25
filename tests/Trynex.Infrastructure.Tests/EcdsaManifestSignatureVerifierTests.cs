using System.Security.Cryptography;
using Trynex.Infrastructure.Security;

namespace Trynex.Infrastructure.Tests;

public sealed class EcdsaManifestSignatureVerifierTests
{
    [Fact]
    public void Verify_AcceptsSignatureFromMatchingPrivateKey()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicKey = signer.ExportSubjectPublicKeyInfoPem();
        var payload = "trusted manifest"u8.ToArray();
        var signature = signer.SignData(
            payload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        using var verifier = new EcdsaManifestSignatureVerifier(publicKey);

        Assert.True(verifier.Verify(payload, signature));
        Assert.False(verifier.Verify("tampered manifest"u8.ToArray(), signature));
    }
}
