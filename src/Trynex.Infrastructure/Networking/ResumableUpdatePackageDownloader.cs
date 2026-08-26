using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Trynex.Core.Abstractions;
using Trynex.Core.Updates;

namespace Trynex.Infrastructure.Networking;

public sealed class ResumableUpdatePackageDownloader : IUpdatePackageDownloader
{
    private const int BufferSize = 128 * 1024;
    private readonly HttpClient _httpClient;

    public ResumableUpdatePackageDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task DownloadAsync(
        Uri source,
        string destinationPath,
        long expectedSize,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureHttps(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (expectedSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedSize), "The expected package size must be positive.");
        }

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var partialPath = fullDestinationPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath)!);

        var existingLength = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0;
        if (existingLength > expectedSize)
        {
            File.Delete(partialPath);
            existingLength = 0;
        }

        if (existingLength == expectedSize)
        {
            File.Move(partialPath, fullDestinationPath, true);
            progress?.Report(new(expectedSize, expectedSize, 0));
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var append = existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (append)
        {
            var contentRange = response.Content.Headers.ContentRange;
            if (contentRange?.From != existingLength ||
                contentRange.To is null ||
                contentRange.To >= expectedSize ||
                contentRange.Length != expectedSize)
            {
                throw new InvalidDataException("The update server returned an invalid byte range.");
            }
        }
        else if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidDataException("The update server did not return a complete package or a valid byte range.");
        }

        if (!append)
        {
            existingLength = 0;
        }

        var responseLength = response.Content.Headers.ContentLength;
        if (responseLength is not null && existingLength + responseLength > expectedSize)
        {
            throw new InvalidDataException("The update package is larger than declared in the signed manifest.");
        }

        await using var sourceStream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = new FileStream(
            partialPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var stopwatch = Stopwatch.StartNew();
        var receivedThisRequest = 0L;
        var buffer = new byte[BufferSize];

        while (true)
        {
            var read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            receivedThisRequest += read;
            var totalReceived = existingLength + receivedThisRequest;
            if (totalReceived > expectedSize)
            {
                destination.Close();
                File.Delete(partialPath);
                throw new InvalidDataException("The update package exceeded its signed size while downloading.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            var bytesPerSecond = stopwatch.Elapsed.TotalSeconds <= 0
                ? 0
                : receivedThisRequest / stopwatch.Elapsed.TotalSeconds;
            progress?.Report(new(totalReceived, expectedSize, bytesPerSecond));
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);

        var finalLength = existingLength + receivedThisRequest;
        if (finalLength != expectedSize)
        {
            throw new EndOfStreamException("The update download ended before the complete package was received.");
        }

        destination.Close();
        File.Move(partialPath, fullDestinationPath, true);
    }

    private static void EnsureHttps(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Update packages must be downloaded from an absolute HTTPS URL.", nameof(uri));
        }
    }
}
