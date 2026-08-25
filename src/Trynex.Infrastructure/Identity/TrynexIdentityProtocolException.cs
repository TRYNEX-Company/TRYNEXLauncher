namespace Trynex.Infrastructure.Identity;

public sealed class TrynexIdentityProtocolException : Exception
{
    public TrynexIdentityProtocolException(string message)
        : base(message)
    {
    }

    public TrynexIdentityProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
