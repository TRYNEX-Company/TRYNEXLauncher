using System.Net;
using System.Text.Json;
using Trynex.Core.Abstractions;
using Trynex.Core.News;

namespace Trynex.Infrastructure.News;

public sealed class CachedJsonNewsFeedStore : INewsFeedStore
{
    private const int MaximumFeedSizeBytes = 512 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _remoteFeedUri;
    private readonly string _cachePath;
    private readonly string _bundledFeedPath;
    private readonly NewsFeedValidator _validator;

    public CachedJsonNewsFeedStore(
        HttpClient httpClient,
        Uri remoteFeedUri,
        string cachePath,
        string bundledFeedPath,
        NewsFeedValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(remoteFeedUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(bundledFeedPath);

        if (!remoteFeedUri.IsAbsoluteUri || remoteFeedUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The remote news feed must use an absolute HTTPS address.", nameof(remoteFeedUri));
        }

        _httpClient = httpClient;
        _remoteFeedUri = remoteFeedUri;
        _cachePath = Path.GetFullPath(cachePath);
        _bundledFeedPath = Path.GetFullPath(bundledFeedPath);
        _validator = validator ?? new NewsFeedValidator();
    }

    public async Task<NewsFeed> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var remoteFeed = await LoadRemoteAsync(cancellationToken).ConfigureAwait(false);
            await TryWriteCacheAsync(remoteFeed, cancellationToken).ConfigureAwait(false);
            return remoteFeed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsRecoverableRemoteFailure(exception))
        {
            // Offline use and temporary server failures fall back to the last trusted JSON document.
        }

        try
        {
            return await LoadFileAsync(_cachePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFileFailure(exception))
        {
            return await LoadFileAsync(_bundledFeedPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<NewsFeed> LoadRemoteAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(_remoteFeedUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidDataException("The remote news feed does not exist yet.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumFeedSizeBytes)
        {
            throw new InvalidDataException("The remote news feed exceeds the allowed size.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        while (true)
        {
            var bytesRead = await contentStream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (buffer.Length + bytesRead > MaximumFeedSizeBytes)
            {
                throw new InvalidDataException("The remote news feed exceeds the allowed size.");
            }

            buffer.Write(chunk, 0, bytesRead);
        }

        return DeserializeAndValidate(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
    }

    private async Task<NewsFeed> LoadFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (stream.Length > MaximumFeedSizeBytes)
        {
            throw new InvalidDataException("The news feed exceeds the allowed size.");
        }

        var feed = await JsonSerializer
            .DeserializeAsync<NewsFeed>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return Validate(feed);
    }

    private NewsFeed DeserializeAndValidate(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return Validate(JsonSerializer.Deserialize<NewsFeed>(bytes, SerializerOptions));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The news feed contains invalid JSON.", exception);
        }
    }

    private NewsFeed Validate(NewsFeed? feed)
    {
        if (feed is null)
        {
            throw new InvalidDataException("The news feed is empty.");
        }

        var validation = _validator.Validate(feed);
        if (!validation.IsValid)
        {
            var details = string.Join(
                "; ",
                validation.Errors.Select(error => $"{error.Code}: {error.RelativePath ?? error.Message}"));
            throw new InvalidDataException($"The news feed is invalid. {details}");
        }

        return feed;
    }

    private async Task TryWriteCacheAsync(NewsFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _cachePath + ".tmp";
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, feed, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, _cachePath, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read-only profile must not make an otherwise valid remote feed unusable.
        }
    }

    private static bool IsRecoverableRemoteFailure(Exception exception) => exception is
        HttpRequestException or
        TaskCanceledException or
        InvalidDataException or
        JsonException or
        IOException or
        UnauthorizedAccessException;

    private static bool IsRecoverableFileFailure(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        InvalidDataException or
        JsonException;
}
