using System.Net.Http.Headers;
using System.Text.Json;
using Trynex.Core.Abstractions;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Networking;

public sealed class JsonUpdateManifestClient : IUpdateManifestClient
{
    private const int MaximumManifestSize = 256 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public JsonUpdateManifestClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LauncherUpdateManifest> GetAsync(
        Uri manifestUri,
        CancellationToken cancellationToken = default)
    {
        EnsureHttps(manifestUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumManifestSize)
        {
            throw new InvalidDataException("The update manifest exceeds the maximum allowed size.");
        }

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var buffer = new MemoryStream();

        var chunk = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumManifestSize)
            {
                throw new InvalidDataException("The update manifest exceeds the maximum allowed size.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        var manifest = await JsonSerializer
            .DeserializeAsync<LauncherUpdateManifest>(buffer, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return manifest ?? throw new InvalidDataException("The update manifest is empty or invalid JSON.");
    }

    private static void EnsureHttps(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update manifests must be downloaded from an absolute HTTPS URL.", nameof(uri));
        }
    }
}
