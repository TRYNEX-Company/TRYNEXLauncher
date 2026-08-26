using System.Net;
using System.Security.Cryptography;
using System.Text;
using Trynex.Infrastructure.Identity;

namespace Trynex.Infrastructure.Tests;

public sealed class TrynexIdentityClientTests
{
    private const string DiscoveryJson = """
        {
          "issuer": "https://id.trynex.dev/api/auth",
          "authorization_endpoint": "https://id.trynex.dev/api/auth/oauth2/authorize",
          "token_endpoint": "https://id.trynex.dev/api/auth/oauth2/token",
          "userinfo_endpoint": "https://id.trynex.dev/api/auth/oauth2/userinfo",
          "revocation_endpoint": "https://id.trynex.dev/api/auth/oauth2/revoke",
          "code_challenge_methods_supported": ["S256"],
          "token_endpoint_auth_methods_supported": ["none", "client_secret_post"]
        }
        """;

    [Fact]
    public async Task SignInAsync_UsesSystemBrowserPkceAndStoresVerifiedProfile()
    {
        string? challenge = null;
        var receiver = new FakeReceiver(authorizationUri =>
        {
            var query = ParseForm(authorizationUri.Query.TrimStart('?'));
            Assert.Equal("code", query["response_type"]);
            Assert.Equal("trynex-launcher", query["client_id"]);
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.Equal(receiverRedirect.AbsoluteUri, query["redirect_uri"]);
            Assert.False(query.ContainsKey("client_secret"));
            challenge = query["code_challenge"];
            return new Uri(
                $"{receiverRedirect.AbsoluteUri}?code=authorization-code&state={Uri.EscapeDataString(query["state"])}&iss={Uri.EscapeDataString("https://id.trynex.dev/api/auth")}");
        });
        var tokenStore = new MemoryTokenStore();
        using var httpClient = new HttpClient(new AsyncDelegateHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json(DiscoveryJson);
            }
            if (request.RequestUri.AbsolutePath == "/api/auth/oauth2/token")
            {
                var form = ParseForm(await request.Content!.ReadAsStringAsync());
                Assert.Equal("authorization_code", form["grant_type"]);
                Assert.Equal("trynex-launcher", form["client_id"]);
                Assert.Equal("authorization-code", form["code"]);
                Assert.DoesNotContain("client_secret", form.Keys);
                Assert.Equal(challenge, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]))));
                return Json("""
                    {"access_token":"access","refresh_token":"refresh","id_token":"header.payload.signature","token_type":"Bearer","expires_in":600,"scope":"openid profile email offline_access"}
                    """);
            }
            if (request.RequestUri.AbsolutePath == "/api/auth/oauth2/userinfo")
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("access", request.Headers.Authorization?.Parameter);
                return Json("""
                    {"sub":"user-1","name":"TRYNEX User","email":"user@example.com","email_verified":true}
                    """);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        var client = CreateClient(httpClient, tokenStore, receiver);

        var profile = await client.SignInAsync();

        Assert.Equal("user-1", profile.Subject);
        Assert.Equal("TRYNEX User", profile.DisplayName);
        Assert.True(profile.EmailVerified);
        Assert.Equal("refresh", tokenStore.Tokens?.RefreshToken);
    }

    [Fact]
    public async Task SignInAsync_RejectsCallbackWithWrongStateBeforeTokenExchange()
    {
        var tokenRequested = false;
        var receiver = new FakeReceiver(_ => new Uri(
            $"{receiverRedirect.AbsoluteUri}?code=stolen&state=wrong&iss={Uri.EscapeDataString("https://id.trynex.dev/api/auth")}"));
        using var httpClient = new HttpClient(new AsyncDelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Task.FromResult(Json(DiscoveryJson));
            }
            tokenRequested = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }));
        var client = CreateClient(httpClient, new MemoryTokenStore(), receiver);

        await Assert.ThrowsAsync<TrynexIdentityProtocolException>(() => client.SignInAsync());

        Assert.False(tokenRequested);
    }

    [Fact]
    public async Task RestoreAsync_RotatesExpiredRefreshTokenBeforeLoadingProfile()
    {
        var tokenStore = new MemoryTokenStore
        {
            Tokens = new IdentityTokenSet(
                "expired-access",
                "old-refresh",
                "old.id.token",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                "openid profile email offline_access")
        };
        using var httpClient = new HttpClient(new AsyncDelegateHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json(DiscoveryJson);
            }
            if (request.RequestUri.AbsolutePath == "/api/auth/oauth2/token")
            {
                var form = ParseForm(await request.Content!.ReadAsStringAsync());
                Assert.Equal("refresh_token", form["grant_type"]);
                Assert.Equal("old-refresh", form["refresh_token"]);
                return Json("""
                    {"access_token":"new-access","refresh_token":"new-refresh","id_token":"new.id.token","token_type":"Bearer","expires_in":600,"scope":"openid profile email offline_access"}
                    """);
            }
            Assert.Equal("new-access", request.Headers.Authorization?.Parameter);
            return Json("""
                {"sub":"user-1","name":"Restored User","email":"restored@example.com","email_verified":true}
                """);
        }));
        var client = CreateClient(httpClient, tokenStore, new FakeReceiver(_ => throw new InvalidOperationException()));

        var profile = await client.RestoreAsync();

        Assert.Equal("Restored User", profile?.DisplayName);
        Assert.Equal("new-refresh", tokenStore.Tokens?.RefreshToken);
    }

    [Fact]
    public async Task SignOutAsync_RevokesRefreshTokenWithoutClientSecretAndClearsLocalTokens()
    {
        var tokenStore = new MemoryTokenStore
        {
            Tokens = new IdentityTokenSet(
                "access",
                "refresh-to-revoke",
                "header.payload.signature",
                "Bearer",
                DateTimeOffset.UtcNow.AddMinutes(5),
                "openid profile email offline_access")
        };
        var revoked = false;
        using var httpClient = new HttpClient(new AsyncDelegateHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json(DiscoveryJson);
            }
            Assert.Equal("/api/auth/oauth2/revoke", request.RequestUri.AbsolutePath);
            var form = ParseForm(await request.Content!.ReadAsStringAsync());
            Assert.Equal("trynex-launcher", form["client_id"]);
            Assert.Equal("refresh-to-revoke", form["token"]);
            Assert.Equal("refresh_token", form["token_type_hint"]);
            Assert.DoesNotContain("client_secret", form.Keys);
            revoked = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var client = CreateClient(httpClient, tokenStore, new FakeReceiver(_ => throw new InvalidOperationException()));

        await client.SignOutAsync();

        Assert.True(revoked);
        Assert.Null(tokenStore.Tokens);
    }

    private static readonly Uri receiverRedirect =
        new("http://127.0.0.1:55123/trynex-launcher/callback/");

    private static TrynexIdentityClient CreateClient(
        HttpClient httpClient,
        MemoryTokenStore store,
        IIdentityAuthorizationReceiver receiver) =>
        new(httpClient, store, _ => Task.FromResult(receiver));

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static Dictionary<string, string> ParseForm(string value) => value
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => part.Split('=', 2))
        .ToDictionary(
            pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
            pair => Uri.UnescapeDataString(pair.ElementAtOrDefault(1)?.Replace('+', ' ') ?? string.Empty),
            StringComparer.Ordinal);

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class FakeReceiver(Func<Uri, Uri> callback) : IIdentityAuthorizationReceiver
    {
        public Uri RedirectUri => receiverRedirect;

        public Task<Uri> ReceiveAsync(Uri authorizationUri, CancellationToken cancellationToken = default) =>
            Task.FromResult(callback(authorizationUri));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MemoryTokenStore : IIdentityTokenStore
    {
        public IdentityTokenSet? Tokens { get; set; }

        public Task<IdentityTokenSet?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Tokens);

        public Task SaveAsync(IdentityTokenSet tokens, CancellationToken cancellationToken = default)
        {
            Tokens = tokens;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Tokens = null;
            return Task.CompletedTask;
        }
    }

    private sealed class AsyncDelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request);
    }
}
