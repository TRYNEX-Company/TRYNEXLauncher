using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Trynex.Infrastructure.Identity;

namespace Trynex.Launcher.Services;

public sealed class DpapiIdentityTokenStore : IIdentityTokenStore
{
    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes(
        "TRYNEX Launcher identity tokens v1"));
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _path;

    public DpapiIdentityTokenStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TRYNEX",
            "Launcher",
            "secure",
            "identity.bin");
    }

    public async Task<IdentityTokenSet?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var file = new FileInfo(_path);
            if (file.Length is <= 0 or > 128 * 1024)
            {
                return null;
            }

            var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            var json = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var tokens = JsonSerializer.Deserialize<IdentityTokenSet>(json, JsonOptions);
                return IsValid(tokens) ? tokens : null;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IdentityTokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        if (!IsValid(tokens))
        {
            throw new ArgumentException("Identity token set is invalid.", nameof(tokens));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path)
                ?? throw new InvalidOperationException("Identity token path has no parent directory.");
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions);
            byte[] encrypted;
            try
            {
                encrypted = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }

            var temporaryPath = _path + ".tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
                File.Move(temporaryPath, _path, true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(encrypted);
                TryDelete(temporaryPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TryDelete(_path);
            TryDelete(_path + ".tmp");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsValid(IdentityTokenSet? tokens) =>
        tokens is not null &&
        tokens.AccessToken.Length is > 0 and <= 16 * 1024 &&
        tokens.RefreshToken?.Length <= 16 * 1024 &&
        tokens.IdToken?.Length <= 32 * 1024 &&
        string.Equals(tokens.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) &&
        tokens.Scope.Length <= 4 * 1024;

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Clearing a local cache is best effort. A still-present file remains
            // protected by Windows DPAPI for the current Windows user only.
        }
    }
}
