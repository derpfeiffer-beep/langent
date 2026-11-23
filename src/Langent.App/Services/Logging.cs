using System.Globalization;
using System.IO;
using System.Text;

namespace Langent.App.Services;

/// <summary>
/// Minimal technical logger (SPEC §9). Writes only technical events — start,
/// triggers, model/token/error codes, durations — never texts or secrets.
/// Rotates <c>log.txt</c> to <c>log.old.txt</c> once it grows past 1 MB.
/// </summary>
public sealed class Logging
{
    private const long MaxBytes = 1024 * 1024;

    private readonly string _logFilePath;
    private readonly string _oldLogFilePath;
    private readonly object _gate = new();

    public Logging(string logFilePath)
    {
        _logFilePath = logFilePath;
        _oldLogFilePath = Path.Combine(
            Path.GetDirectoryName(logFilePath) ?? ".",
            "log.old.txt");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");

        lock (_gate)
        {
            try
            {
                RotateIfTooLarge();
                File.AppendAllText(_logFilePath, line, Encoding.UTF8);
            }
            catch
            {
                // Logging must never crash the app.
            }
        }
    }

    private void RotateIfTooLarge()
    {
        var info = new FileInfo(_logFilePath);
        if (!info.Exists || info.Length <= MaxBytes)
        {
            return;
        }

        if (File.Exists(_oldLogFilePath))
        {
            File.Delete(_oldLogFilePath);
        }

        File.Move(_logFilePath, _oldLogFilePath);
    }
}
