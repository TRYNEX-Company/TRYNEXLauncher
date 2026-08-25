using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Trynex.Infrastructure.Identity;

namespace Trynex.Launcher.Services;

public sealed class SystemBrowserIdentityReceiver : IIdentityAuthorizationReceiver
{
    private const string CallbackPath = "trynex-launcher/callback/";
    private readonly HttpListener _listener;
    private bool _disposed;

    private SystemBrowserIdentityReceiver(HttpListener listener, Uri redirectUri)
    {
        _listener = listener;
        RedirectUri = redirectUri;
    }

    public Uri RedirectUri { get; }

    public static SystemBrowserIdentityReceiver Create()
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var port = RandomNumberGenerator.GetInt32(49_152, 65_535);
            var redirectUri = new Uri($"http://127.0.0.1:{port}/{CallbackPath}");
            var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri.AbsoluteUri);
            try
            {
                listener.Start();
                return new SystemBrowserIdentityReceiver(listener, redirectUri);
            }
            catch (HttpListenerException)
            {
                listener.Close();
            }
        }

        throw new TrynexIdentityProtocolException("Не удалось открыть локальный канал возврата для TRYNEX ID.");
    }

    public async Task<Uri> ReceiveAsync(Uri authorizationUri, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = authorizationUri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new TrynexIdentityProtocolException(
                "Не удалось открыть системный браузер для входа в TRYNEX ID.",
                exception);
        }

        HttpListenerContext context;
        try
        {
            context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpListenerException exception)
        {
            throw new TrynexIdentityProtocolException("Локальный канал входа TRYNEX ID был закрыт.", exception);
        }

        var callbackUri = context.Request.Url
            ?? throw new TrynexIdentityProtocolException("Браузер вернул неверный адрес TRYNEX ID.");
        await WriteBrowserResponseAsync(context.Response, cancellationToken).ConfigureAwait(false);
        return callbackUri;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _listener.Close();
        }
        return ValueTask.CompletedTask;
    }

    private static async Task WriteBrowserResponseAsync(
        HttpListenerResponse response,
        CancellationToken cancellationToken)
    {
        const string html = """
            <!doctype html>
            <html lang="ru">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'; base-uri 'none'">
              <title>TRYNEX ID</title>
              <style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#08090d;color:#f4f2ff;font-family:system-ui}.card{max-width:420px;margin:24px;padding:28px;border:1px solid #292a35;border-radius:20px;background:#111218;text-align:center}h1{font-size:25px}p{color:#aaa9b5;line-height:1.55}</style>
            </head>
            <body><main class="card"><h1>Возвращаемся в TRYNEX</h1><p>Окно можно закрыть. Лаунчер завершает безопасную проверку входа.</p></main></body>
            </html>
            """;
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or HttpListenerException)
        {
            // The user may close the browser tab immediately after the redirect.
        }
        finally
        {
            response.Close();
        }
    }
}
