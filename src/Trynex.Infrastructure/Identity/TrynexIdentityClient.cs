using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trynex.Core.Abstractions;
using Trynex.Core.Identity;

namespace Trynex.Infrastructure.Identity;

public sealed class TrynexIdentityClient : ITrynexIdentityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IIdentityTokenStore _tokenStore;
    private readonly Func<CancellationToken, Task<IIdentityAuthorizationReceiver>> _receiverFactory;
    private readonly TrynexIdentityOptions _options;
    private ProviderConfiguration? _configuration;

    public TrynexIdentityClient(
        HttpClient httpClient,
        IIdentityTokenStore tokenStore,
        Func<CancellationToken, Task<IIdentityAuthorizationReceiver>> receiverFactory,
        TrynexIdentityOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _receiverFactory = receiverFactory ?? throw new ArgumentNullException(nameof(receiverFactory));
        _options = options ?? TrynexIdentityOptions.Production;
        ValidateOptions(_options);
    }

    public async Task<TrynexIdentityProfile?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            return null;
        }

        try
        {
            var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            tokens = await RefreshIfNeededAsync(configuration, tokens, cancellationToken).ConfigureAwait(false);
            return await GetProfileAsync(configuration, tokens.AccessToken, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TrynexIdentityProtocolException)
        {
            // A cached token is never treated as proof of identity when it cannot
            // be confirmed by TRYNEX ID. Invalid/expired credentials are removed.
            await _tokenStore.ClearAsync(CancellationToken.None).ConfigureAwait(false);
            return null;
        }
    }

    public async Task<TrynexIdentityProfile> SignInAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.InteractiveTimeout);
        var token = timeout.Token;

        var configuration = await GetConfigurationAsync(token).ConfigureAwait(false);
        await using var receiver = await _receiverFactory(token).ConfigureAwait(false);
        ValidateRedirectUri(receiver.RedirectUri);

        var state = CreateRandomBase64Url(32);
        var nonce = CreateRandomBase64Url(32);
        var verifier = CreateRandomBase64Url(64);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = BuildAuthorizationUri(
            configuration.AuthorizationEndpoint,
            receiver.RedirectUri,
            state,
            nonce,
            challenge);

        var callbackUri = await receiver.ReceiveAsync(authorizationUri, token).ConfigureAwait(false);
        var parameters = ParseQuery(callbackUri);
        ValidateAuthorizationResponse(parameters, state, configuration.Issuer);

        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID не вернул код авторизации.");
        }

        var tokens = await ExchangeCodeAsync(
            configuration,
            code,
            verifier,
            receiver.RedirectUri,
            token).ConfigureAwait(false);
        var profile = await GetProfileAsync(configuration, tokens.AccessToken, token).ConfigureAwait(false);
        await _tokenStore.SaveAsync(tokens, token).ConfigureAwait(false);
        return profile;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(tokens?.RefreshToken))
            {
                var configuration = await GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _options.ClientId,
                    ["token"] = tokens.RefreshToken,
                    ["token_type_hint"] = "refresh_token",
                });
                using var response = await _httpClient
                    .PostAsync(configuration.RevocationEndpoint, content, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new TrynexIdentityProtocolException("TRYNEX ID не подтвердил выход из аккаунта.");
                }
            }
        }
        finally
        {
            // Local credentials must be removed even if the identity service is
            // temporarily unavailable. The server token normally expires within
            // 30 days and can also be revoked from the account security page.
            await _tokenStore.ClearAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<ProviderConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        if (_configuration is not null)
        {
            return _configuration;
        }

        var discoveryUri = new Uri(_options.Authority, "/.well-known/openid-configuration");
        using var response = await _httpClient
            .GetAsync(discoveryUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new TrynexIdentityProtocolException("Не удалось получить настройки TRYNEX ID.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var configuration = new ProviderConfiguration(
            ReadUri(root, "issuer"),
            ReadUri(root, "authorization_endpoint"),
            ReadUri(root, "token_endpoint"),
            ReadUri(root, "userinfo_endpoint"),
            ReadUri(root, "revocation_endpoint"));

        ValidateProviderUri(configuration.Issuer, allowPathOutsideAuthApi: true);
        ValidateProviderUri(configuration.AuthorizationEndpoint);
        ValidateProviderUri(configuration.TokenEndpoint);
        ValidateProviderUri(configuration.UserInfoEndpoint);
        ValidateProviderUri(configuration.RevocationEndpoint);

        if (!ReadStringArray(root, "code_challenge_methods_supported").Contains("S256", StringComparer.Ordinal))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID не объявил обязательную поддержку PKCE S256.");
        }

        if (!ReadStringArray(root, "token_endpoint_auth_methods_supported").Contains("none", StringComparer.Ordinal))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID не разрешает безопасный вход публичного клиента.");
        }

        _configuration = configuration;
        return configuration;
    }

    private Uri BuildAuthorizationUri(
        Uri endpoint,
        Uri redirectUri,
        string state,
        string nonce,
        string challenge)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["scope"] = _options.Scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        };
        return AppendQuery(endpoint, parameters);
    }

    private async Task<IdentityTokenSet> ExchangeCodeAsync(
        ProviderConfiguration configuration,
        string code,
        string verifier,
        Uri redirectUri,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = _options.ClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
        });
        return await RequestTokensAsync(configuration.TokenEndpoint, content, null, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IdentityTokenSet> RefreshIfNeededAsync(
        ProviderConfiguration configuration,
        IdentityTokenSet tokens,
        CancellationToken cancellationToken)
    {
        if (tokens.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return tokens;
        }

        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new TrynexIdentityProtocolException("Сессия TRYNEX ID истекла.");
        }

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["refresh_token"] = tokens.RefreshToken,
        });
        var refreshed = await RequestTokensAsync(
            configuration.TokenEndpoint,
            content,
            tokens,
            cancellationToken).ConfigureAwait(false);
        await _tokenStore.SaveAsync(refreshed, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    private async Task<IdentityTokenSet> RequestTokensAsync(
        Uri endpoint,
        HttpContent content,
        IdentityTokenSet? previous,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadString(root, "error_description") ?? TryReadString(root, "error");
            throw new TrynexIdentityProtocolException(error is null
                ? "TRYNEX ID отклонил запрос токена."
                : $"TRYNEX ID отклонил запрос: {error}");
        }

        var accessToken = RequiredString(root, "access_token");
        var tokenType = RequiredString(root, "token_type");
        if (!string.Equals(tokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID вернул неподдерживаемый тип токена.");
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt64(out var seconds)
            ? seconds
            : 0;
        if (expiresIn is <= 0 or > 86_400)
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID вернул неверный срок токена.");
        }

        var refreshToken = TryReadString(root, "refresh_token") ?? previous?.RefreshToken;
        var idToken = TryReadString(root, "id_token") ?? previous?.IdToken;
        if (previous is null && (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(idToken)))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID не выдал полную OIDC-сессию.");
        }

        return new IdentityTokenSet(
            accessToken,
            refreshToken,
            idToken,
            "Bearer",
            DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            TryReadString(root, "scope") ?? previous?.Scope ?? _options.Scope);
    }

    private async Task<TrynexIdentityProfile> GetProfileAsync(
        ProviderConfiguration configuration,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, configuration.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new TrynexIdentityProtocolException("Сессия TRYNEX ID недействительна.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new TrynexIdentityProtocolException("Не удалось проверить профиль TRYNEX ID.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var subject = RequiredString(root, "sub");
        var name = TryReadString(root, "name") ?? TryReadString(root, "preferred_username") ?? "TRYNEX User";
        var email = RequiredString(root, "email");
        var emailVerified = root.TryGetProperty("email_verified", out var verified) && verified.ValueKind == JsonValueKind.True;
        var pictureText = TryReadString(root, "picture");
        var picture = Uri.TryCreate(pictureText, UriKind.Absolute, out var parsedPicture) && parsedPicture.Scheme == Uri.UriSchemeHttps
            ? parsedPicture
            : null;
        return new TrynexIdentityProfile(subject, name, email, emailVerified, picture);
    }

    private void ValidateAuthorizationResponse(
        IReadOnlyDictionary<string, string> parameters,
        string expectedState,
        Uri expectedIssuer)
    {
        if (parameters.TryGetValue("error", out var error))
        {
            var description = parameters.GetValueOrDefault("error_description");
            throw new TrynexIdentityProtocolException(description is null
                ? $"Вход отменён: {error}."
                : $"Вход отменён: {description}");
        }

        if (!parameters.TryGetValue("state", out var actualState) || !FixedTimeEquals(expectedState, actualState))
        {
            throw new TrynexIdentityProtocolException("Проверка состояния входа не пройдена.");
        }

        if (!parameters.TryGetValue("iss", out var issuerText) ||
            !Uri.TryCreate(issuerText, UriKind.Absolute, out var issuer) ||
            issuer != expectedIssuer)
        {
            throw new TrynexIdentityProtocolException("Ответ получен не от ожидаемого TRYNEX ID.");
        }
    }

    private void ValidateProviderUri(Uri uri, bool allowPathOutsideAuthApi = false)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != _options.Authority.Scheme ||
            !string.Equals(uri.Host, _options.Authority.Host, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != _options.Authority.Port || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID объявил недоверенную конечную точку.");
        }

        if (!allowPathOutsideAuthApi && !uri.AbsolutePath.StartsWith("/api/auth/", StringComparison.Ordinal))
        {
            throw new TrynexIdentityProtocolException("TRYNEX ID объявил неожиданную конечную точку.");
        }
    }

    private static void ValidateOptions(TrynexIdentityOptions options)
    {
        if (!options.Authority.IsAbsoluteUri || string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("TRYNEX ID options are invalid.", nameof(options));
        }

        var secure = options.Authority.Scheme == Uri.UriSchemeHttps;
        var loopbackDevelopment = options.Authority.Scheme == Uri.UriSchemeHttp && options.Authority.IsLoopback;
        if (!secure && !loopbackDevelopment)
        {
            throw new ArgumentException("TRYNEX ID authority must use HTTPS.", nameof(options));
        }
    }

    private static void ValidateRedirectUri(Uri redirectUri)
    {
        if (!redirectUri.IsAbsoluteUri || redirectUri.Scheme != Uri.UriSchemeHttp ||
            redirectUri.Host != "127.0.0.1" ||
            redirectUri.AbsolutePath != "/trynex-launcher/callback/" ||
            !string.IsNullOrEmpty(redirectUri.Query) || !string.IsNullOrEmpty(redirectUri.Fragment))
        {
            throw new TrynexIdentityProtocolException("Локальный адрес возврата TRYNEX ID недействителен.");
        }
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(Uri uri)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = DecodeQueryPart(separator < 0 ? part : part[..separator]);
            var value = DecodeQueryPart(separator < 0 ? string.Empty : part[(separator + 1)..]);
            if (!result.TryAdd(key, value))
            {
                throw new TrynexIdentityProtocolException("TRYNEX ID вернул неоднозначный ответ.");
            }
        }
        return result;
    }

    private static Uri AppendQuery(Uri uri, IReadOnlyDictionary<string, string> values)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        var query = string.Join("&", values.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri(uri.AbsoluteUri + separator + query);
    }

    private static string DecodeQueryPart(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string CreateRandomBase64Url(int byteCount) =>
        Base64Url(RandomNumberGenerator.GetBytes(byteCount));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static Uri ReadUri(JsonElement root, string propertyName)
    {
        var value = RequiredString(root, propertyName);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new TrynexIdentityProtocolException($"TRYNEX ID вернул неверное поле {propertyName}.");
    }

    private static string RequiredString(JsonElement root, string propertyName) =>
        TryReadString(root, propertyName) ??
        throw new TrynexIdentityProtocolException($"TRYNEX ID не вернул обязательное поле {propertyName}.");

    private static string? TryReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!)
                .ToArray()
            : [];

    private sealed record ProviderConfiguration(
        Uri Issuer,
        Uri AuthorizationEndpoint,
        Uri TokenEndpoint,
        Uri UserInfoEndpoint,
        Uri RevocationEndpoint);
}
