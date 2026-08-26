using System.IO;

namespace Trynex.Bootstrapper;

internal sealed class BootstrapperLogger
{
    private readonly string _logPath;

    public BootstrapperLogger(string logPath)
    {
        _logPath = logPath;
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message} {exception.GetType().Name}: {exception.Message}");
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            File.AppendAllText(
                _logPath,
                $"{DateTimeOffset.UtcNow:O} [{level}] {message}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging must never prevent the current launcher version from starting.
        }
    }
}
