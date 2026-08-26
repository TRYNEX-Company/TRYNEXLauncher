namespace Trynex.Infrastructure.Identity;

public interface IIdentityAuthorizationReceiver : IAsyncDisposable
{
    Uri RedirectUri { get; }

    Task<Uri> ReceiveAsync(Uri authorizationUri, CancellationToken cancellationToken = default);
}
