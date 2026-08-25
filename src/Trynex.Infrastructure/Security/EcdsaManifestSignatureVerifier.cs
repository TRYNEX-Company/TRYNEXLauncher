using System.Security.Cryptography;
using Trynex.Core.Abstractions;

namespace Trynex.Infrastructure.Security;

public sealed class EcdsaManifestSignatureVerifier : IManifestSignatureVerifier, IDisposable
{
    private readonly ECDsa _ecdsa;

    public EcdsaManifestSignatureVerifier(string publicKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);

        _ecdsa = ECDsa.Create();
        _ecdsa.ImportFromPem(publicKeyPem);

        if (_ecdsa.KeySize != 256)
        {
            _ecdsa.Dispose();
            throw new CryptographicException("The manifest public key must use ECDSA P-256.");
        }
    }

    public bool Verify(ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> signature)
    {
        return _ecdsa.VerifyData(
            payload.Span,
            signature.Span,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
    }

    public void Dispose()
    {
        _ecdsa.Dispose();
    }
}
