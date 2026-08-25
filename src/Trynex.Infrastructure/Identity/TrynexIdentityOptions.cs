namespace Trynex.Infrastructure.Identity;

public sealed record TrynexIdentityOptions
{
    public static TrynexIdentityOptions Production { get; } = new();

    public Uri Authority { get; init; } = new("https://id.trynex.dev");

    public string ClientId { get; init; } = "trynex-launcher";

    public string Scope { get; init; } = "openid profile email offline_access";

    public TimeSpan InteractiveTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
