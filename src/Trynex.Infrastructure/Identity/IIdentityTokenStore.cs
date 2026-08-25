namespace Trynex.Infrastructure.Identity;

public interface IIdentityTokenStore
{
    Task<IdentityTokenSet?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IdentityTokenSet tokens, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
