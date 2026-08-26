namespace Trynex.Infrastructure.Identity;

public sealed record IdentityTokenSet(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string Scope);
