namespace Trynex.Core.Identity;

public sealed record TrynexIdentityProfile(
    string Subject,
    string DisplayName,
    string Email,
    bool EmailVerified,
    Uri? Picture);
