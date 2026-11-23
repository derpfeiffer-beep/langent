using System.IO.Pipes;

namespace Langent.App.Services;

/// <summary>
/// Single-instance coordination (SPEC §9). The first instance owns a named
/// mutex and listens on a named pipe; a second launch signals the first over
/// the pipe and exits. The signal handler currently logs the hit — opening the
/// settings window lands with the settings UI (M5).
/// </summary>
public static class SingleInstance
{
    public const string MutexName = @"Local\Langent.SingleInstance";
    public const string PipeName = "Langent.Pipe";

    /// <summary>Best-effort: tell the already-running instance that a second start happened.</summary>
    public static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);
            client.WriteByte(1);
            client.Flush();
        }
        catch
        {
            // The other instance may be shutting down; nothing we can do.
        }
    }
}

/// <summary>
/// Background listener that raises a callback whenever a second instance signals.
/// </summary>
public sealed class SingleInstanceSignalServer : IDisposable
{
    private readonly Logging _log;
    private readonly Action _onSignal;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public SingleInstanceSignalServer(Logging log, Action onSignal)
    {
        _log = log;
        _onSignal = onSignal;
    }

    public void Start() => _loop = Task.Run(() => RunAsync(_cts.Token));

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    SingleInstance.PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                var buffer = new byte[1];
                _ = await server.ReadAsync(buffer, ct).ConfigureAwait(false);

                _onSignal();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error("Single-Instance-Pipe-Fehler", ex);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Expected cancellation noise on shutdown.
        }

        _cts.Dispose();
    }
}
