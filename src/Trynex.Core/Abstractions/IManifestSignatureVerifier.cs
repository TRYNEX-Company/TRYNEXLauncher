namespace Trynex.Core.Abstractions;

public interface IManifestSignatureVerifier
{
    bool Verify(ReadOnlyMemory<byte> payload, ReadOnlyMemory<byte> signature);
}
