using Trynex.Core.Identity;

namespace Trynex.Core.Abstractions;

public interface ITrynexIdentityService
{
    Task<TrynexIdentityProfile?> RestoreAsync(CancellationToken cancellationToken = default);

    Task<TrynexIdentityProfile> SignInAsync(CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
